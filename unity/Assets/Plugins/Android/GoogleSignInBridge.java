package com.stackit.nativeplugin;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import org.json.JSONObject;

import java.util.concurrent.Executors;

/**
 * Sign in with Google through Android Credential Manager. The ID token is issued for the
 * backend's *web* OAuth client ID (serverClientId) and carries the nonce generated in C#.
 * Result JSON goes to NativeCallbacks.OnSignInResult.
 */
public final class GoogleSignInBridge {
    private GoogleSignInBridge() {}

    public static void signIn(final Activity activity, final String webClientId, final String nonce) {
        GetGoogleIdOption option = new GetGoogleIdOption.Builder()
                .setFilterByAuthorizedAccounts(false)
                .setServerClientId(webClientId)
                .setNonce(nonce)
                .build();
        GetCredentialRequest request = new GetCredentialRequest.Builder()
                .addCredentialOption(option)
                .build();

        CredentialManager.create(activity).getCredentialAsync(
                activity,
                request,
                new CancellationSignal(),
                Executors.newSingleThreadExecutor(),
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(GetCredentialResponse response) {
                        Credential credential = response.getCredential();
                        if (credential instanceof CustomCredential
                                && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                            GoogleIdTokenCredential google = GoogleIdTokenCredential.createFrom(credential.getData());
                            reply("id_token", google.getIdToken());
                        } else {
                            reply("error", "unexpected_credential");
                        }
                    }

                    @Override
                    public void onError(GetCredentialException e) {
                        reply("error", e instanceof GetCredentialCancellationException ? "cancelled" : String.valueOf(e.getMessage()));
                    }
                });
    }

    private static void reply(String key, String value) {
        try {
            StackItBridges.send("OnSignInResult", new JSONObject().put(key, value).toString());
        } catch (Exception e) {
            StackItBridges.send("OnSignInResult", "{\"error\":\"json\"}");
        }
    }
}
