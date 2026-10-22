package com.stackit.nativeplugin;

import android.app.Activity;
import android.os.Build;
import android.view.HapticFeedbackConstants;
import android.view.View;

/**
 * Haptics through the view's haptic feedback: no VIBRATE permission needed and it respects the
 * user's system "touch feedback" setting. kind: 0 light, 1 medium, 2 heavy, 3 success.
 */
public final class Haptics {
    private Haptics() {}

    public static void play(final Activity activity, final int kind) {
        activity.runOnUiThread(() -> {
            View view = activity.getWindow().getDecorView();
            int constant;
            switch (kind) {
                case 0:
                    constant = HapticFeedbackConstants.KEYBOARD_TAP;
                    break;
                case 1:
                    constant = HapticFeedbackConstants.VIRTUAL_KEY;
                    break;
                case 3:
                    constant = Build.VERSION.SDK_INT >= 30
                            ? HapticFeedbackConstants.CONFIRM
                            : HapticFeedbackConstants.LONG_PRESS;
                    break;
                default:
                    constant = HapticFeedbackConstants.LONG_PRESS;
                    break;
            }
            view.performHapticFeedback(constant);
        });
    }
}
