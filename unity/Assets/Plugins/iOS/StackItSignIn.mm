// Sign in with Apple (AuthenticationServices) and Google (GoogleSignIn pod, added through
// Assets/_Project/Editor/StackItDependencies.xml). Results go back as JSON to
// NativeCallbacks.OnSignInResult: {"id_token": "..."} or {"error": "cancelled" | "..."}.

#import <AuthenticationServices/AuthenticationServices.h>
#import <UIKit/UIKit.h>
#import "AppDelegateListener.h"
#if __has_include(<GoogleSignIn/GoogleSignIn.h>)
#import <GoogleSignIn/GoogleSignIn.h>
#define STACKIT_HAS_GOOGLE 1
#endif

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);
extern UIViewController* UnityGetGLViewController(void);

static void StackItSendSignIn(NSDictionary* payload)
{
    NSData* json = [NSJSONSerialization dataWithJSONObject:payload options:0 error:nil];
    NSString* text = [[NSString alloc] initWithData:json encoding:NSUTF8StringEncoding];
    UnitySendMessage("StackItNative", "OnSignInResult", [text UTF8String]);
}

@interface StackItAppleSignIn : NSObject <ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding>
@end

static StackItAppleSignIn* sAppleDelegate = nil; // kept alive until the flow completes

@implementation StackItAppleSignIn

- (void)authorizationController:(ASAuthorizationController*)controller didCompleteWithAuthorization:(ASAuthorization*)authorization
{
    NSString* token = nil;
    if ([authorization.credential isKindOfClass:[ASAuthorizationAppleIDCredential class]])
    {
        NSData* data = ((ASAuthorizationAppleIDCredential*)authorization.credential).identityToken;
        if (data != nil) token = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    }
    StackItSendSignIn(token != nil ? @{ @"id_token": token } : @{ @"error": @"no_token" });
    sAppleDelegate = nil;
}

- (void)authorizationController:(ASAuthorizationController*)controller didCompleteWithError:(NSError*)error
{
    NSString* reason = error.code == ASAuthorizationErrorCanceled ? @"cancelled" : (error.localizedDescription ?: @"failed");
    StackItSendSignIn(@{ @"error": reason });
    sAppleDelegate = nil;
}

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController*)controller
{
    return UnityGetGLViewController().view.window;
}

@end

extern "C" void _StackItSignInApple(const char* hashedNonce)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        ASAuthorizationAppleIDRequest* request = [[[ASAuthorizationAppleIDProvider alloc] init] createRequest];
        request.requestedScopes = @[];
        request.nonce = [NSString stringWithUTF8String:hashedNonce];
        ASAuthorizationController* controller = [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[ request ]];
        sAppleDelegate = [[StackItAppleSignIn alloc] init];
        controller.delegate = sAppleDelegate;
        controller.presentationContextProvider = sAppleDelegate;
        [controller performRequests];
    });
}

extern "C" bool _StackItGoogleAvailable(void)
{
#ifdef STACKIT_HAS_GOOGLE
    return true;
#else
    return false;
#endif
}

extern "C" void _StackItSignInGoogle(const char* clientId)
{
#ifdef STACKIT_HAS_GOOGLE
    NSString* client = [NSString stringWithUTF8String:clientId];
    dispatch_async(dispatch_get_main_queue(), ^{
        GIDSignIn.sharedInstance.configuration = [[GIDConfiguration alloc] initWithClientID:client];
        [GIDSignIn.sharedInstance signInWithPresentingViewController:UnityGetGLViewController()
                                                          completion:^(GIDSignInResult* result, NSError* error) {
            if (error != nil)
            {
                NSString* reason = error.code == kGIDSignInErrorCodeCanceled ? @"cancelled" : (error.localizedDescription ?: @"failed");
                StackItSendSignIn(@{ @"error": reason });
                return;
            }
            NSString* token = result.user.idToken.tokenString;
            StackItSendSignIn(token != nil ? @{ @"id_token": token } : @{ @"error": @"no_token" });
        }];
    });
#else
    StackItSendSignIn(@{ @"error": @"google_sdk_missing" });
#endif
}

#ifdef STACKIT_HAS_GOOGLE
// GoogleSignIn needs the OAuth redirect URL that comes back through the app delegate.
@interface StackItGoogleUrlListener : NSObject <AppDelegateListener>
@end

@implementation StackItGoogleUrlListener

+ (void)load
{
    static StackItGoogleUrlListener* listener;
    listener = [[StackItGoogleUrlListener alloc] init];
    UnityRegisterAppDelegateListener(listener);
}

- (void)onOpenURL:(NSNotification*)notification
{
    NSURL* url = notification.userInfo[@"url"];
    if (url != nil) [GIDSignIn.sharedInstance handleURL:url];
}

@end
#endif
