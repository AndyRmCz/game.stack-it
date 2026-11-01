package com.stackit.nativeplugin;

/*
 * Shared helper for the Android bridges called from C# (Assets/_Project/Scripts/Services/Native):
 * ShareBridge, Haptics, InstallReferrerBridge and GoogleSignInBridge.
 */

import com.unity3d.player.UnityPlayer;

final class StackItBridges {
    static final String CALLBACK_OBJECT = "StackItNative";

    private StackItBridges() {}

    static void send(String method, String message) {
        UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, method, message == null ? "" : message);
    }
}
