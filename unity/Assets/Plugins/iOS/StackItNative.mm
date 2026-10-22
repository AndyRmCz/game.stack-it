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
