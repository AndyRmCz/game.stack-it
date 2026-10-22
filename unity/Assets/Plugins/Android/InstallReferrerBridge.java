package com.stackit.nativeplugin;

import android.app.Activity;
import android.util.Log;

import com.android.installreferrer.api.InstallReferrerClient;
import com.android.installreferrer.api.InstallReferrerStateListener;
import com.android.installreferrer.api.ReferrerDetails;

/**
 * Play Install Referrer: the landing page's Play Store link carries "challenge=CODE", so a
 * fresh install can open the challenge that brought the player in.
 */
public final class InstallReferrerBridge {
    private InstallReferrerBridge() {}

    public static void fetch(Activity activity) {
        final InstallReferrerClient client = InstallReferrerClient.newBuilder(activity).build();
        client.startConnection(new InstallReferrerStateListener() {
            @Override
            public void onInstallReferrerSetupFinished(int responseCode) {
                try {
                    if (responseCode == InstallReferrerClient.InstallReferrerResponse.OK) {
                        ReferrerDetails details = client.getInstallReferrer();
                        StackItBridges.send("OnInstallReferrer", details.getInstallReferrer());
                    }
                } catch (Exception e) {
                    Log.w("StackItReferrer", "install referrer failed", e);
                } finally {
                    client.endConnection();
                }
            }

            @Override
            public void onInstallReferrerServiceDisconnected() {
                // One-shot read at first launch: nothing to retry.
            }
        });
    }
}
