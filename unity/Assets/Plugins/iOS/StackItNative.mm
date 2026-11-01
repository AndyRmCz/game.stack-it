// Stack IT native helpers for iOS: share sheet and haptics.
// Contract: Assets/_Project/Scripts/Services/Native/NativeBridge.cs (IosNative).

#import <UIKit/UIKit.h>

extern UIViewController* UnityGetGLViewController(void);

static NSString* StackItString(const char* value)
{
    return value ? [NSString stringWithUTF8String:value] : @"";
}

extern "C" void _StackItShare(const char* text, const char* url, const char* filePath)
{
    NSString* message = [NSString stringWithFormat:@"%@ %@", StackItString(text), StackItString(url)];
    NSMutableArray* items = [NSMutableArray arrayWithObject:message];
    NSString* path = StackItString(filePath);
    if (path.length > 0 && [[NSFileManager defaultManager] fileExistsAtPath:path])
    {
        [items addObject:[NSURL fileURLWithPath:path]];
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* root = UnityGetGLViewController();
        UIActivityViewController* sheet = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
        // iPad presents the sheet as a popover anchored to the screen centre.
        sheet.popoverPresentationController.sourceView = root.view;
        sheet.popoverPresentationController.sourceRect = CGRectMake(CGRectGetMidX(root.view.bounds), CGRectGetMidY(root.view.bounds), 1, 1);
        sheet.popoverPresentationController.permittedArrowDirections = 0;
        [root presentViewController:sheet animated:YES completion:nil];
    });
}

// Instagram's official "Sharing to Stories" (H5): the challenge card goes to the pasteboard as
// the story's background image, then Instagram opens. Needs the Facebook App ID registered for this app. If
// Instagram can't be opened, falls back to the share sheet.
extern "C" void _StackItShareInstagramStory(const char* facebookAppId, const char* text, const char* url, const char* filePath)
{
    NSString* appId = StackItString(facebookAppId);
    NSString* path = StackItString(filePath);
    NSData* image = path.length > 0 ? [NSData dataWithContentsOfFile:path] : nil;
    if (appId.length == 0 || image == nil)
    {
        _StackItShare(text, url, filePath);
        return;
    }
    // Copied now: the C strings are only valid during this call.
    NSString* fallbackText = StackItString(text);
    NSString* fallbackUrl = StackItString(url);
    NSURL* open = [NSURL URLWithString:[@"instagram-stories://share?source_application=" stringByAppendingString:appId]];
    dispatch_async(dispatch_get_main_queue(), ^{
        NSDictionary* item = @{ @"com.instagram.sharedSticker.backgroundImage": image };
        NSDictionary* options = @{ UIPasteboardOptionExpirationDate: [NSDate dateWithTimeIntervalSinceNow:300] };
        [[UIPasteboard generalPasteboard] setItems:@[ item ] options:options];
        [[UIApplication sharedApplication] openURL:open options:@{} completionHandler:^(BOOL success) {
            if (!success) _StackItShare(fallbackText.UTF8String, fallbackUrl.UTF8String, path.UTF8String);
        }];
    });
}

// kind: 0 light, 1 medium, 2 heavy, 3 success (HapticKind in C#).
extern "C" void _StackItHaptic(int kind)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (kind == 3)
        {
            UINotificationFeedbackGenerator* generator = [[UINotificationFeedbackGenerator alloc] init];
            [generator notificationOccurred:UINotificationFeedbackTypeSuccess];
            return;
        }
        UIImpactFeedbackStyle style = kind == 0 ? UIImpactFeedbackStyleLight
                                    : kind == 1 ? UIImpactFeedbackStyleMedium
                                                : UIImpactFeedbackStyleHeavy;
        UIImpactFeedbackGenerator* generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
        [generator impactOccurred];
    });
}
