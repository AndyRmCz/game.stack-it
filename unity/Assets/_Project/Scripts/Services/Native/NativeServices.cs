using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace StackIt.Services.Native
{
    public sealed class NativeHaptics : IHaptics
    {
        public bool Enabled { get; set; } = true;

        public void Play(HapticKind kind)
        {
            if (!Enabled) return;
            switch (Application.platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    IosNative._StackItHaptic((int)kind);
                    break;
                case RuntimePlatform.Android:
                    using (AndroidJavaObject activity = AndroidNative.Activity())
                        AndroidNative.CallStatic("Haptics", "play", activity, (int)kind);
                    break;
            }
        }
    }

    public sealed class NativeShareService : IShareService
    {
        public void Share(string text, string url, string videoPath)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    IosNative._StackItShare(text, url, videoPath ?? "");
                    break;
                case RuntimePlatform.Android:
                    using (AndroidJavaObject activity = AndroidNative.Activity())
                        AndroidNative.CallStatic("ShareBridge", "share", activity, text + "\n" + url, videoPath ?? "");
                    break;
                default:
                    GUIUtility.systemCopyBuffer = text + " " + url; // editor / desktop: copy the link
                    Debug.Log($"[Share] {text} {url} {videoPath}");
                    break;
            }
        }
    }

    /// <summary>Sign in with Apple (iOS) and Google (Credential Manager on Android, GoogleSignIn on iOS).</summary>
    public sealed class NativeSignInService : ISignInService
    {
        private readonly string _googleWebClientId;
        private readonly string _googleIosClientId;

        public NativeSignInService(string googleWebClientId, string googleIosClientId)
        {
            _googleWebClientId = googleWebClientId;
            _googleIosClientId = googleIosClientId;
        }

        public bool IsAvailable(SignInProvider provider)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    return provider == SignInProvider.Apple ||
                           (!string.IsNullOrEmpty(_googleIosClientId) && IosNative._StackItGoogleAvailable());
                case RuntimePlatform.Android:
                    return provider == SignInProvider.Google && !string.IsNullOrEmpty(_googleWebClientId);
                default:
                    return false;
            }
        }

        public async Task<ProviderToken> SignInAsync(SignInProvider provider)
        {
            if (!IsAvailable(provider)) throw new ApiException(0, "provider_unavailable", "Sign-in is not available");
            string nonce = NewNonce();
            Task<string> result = NativeCallbacks.Instance.Expect("signin");

            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                // Apple embeds SHA-256(nonce) in the token; the backend hashes the raw value to compare.
                if (provider == SignInProvider.Apple) IosNative._StackItSignInApple(Sha256Hex(nonce));
                else
                {
                    IosNative._StackItSignInGoogle(_googleIosClientId);
                    nonce = null; // the iOS GoogleSignIn flow used here does not carry a nonce
                }
            }
            else
            {
                using (AndroidJavaObject activity = AndroidNative.Activity())
                    AndroidNative.CallStatic("GoogleSignInBridge", "signIn", activity, _googleWebClientId, nonce);
            }

            JObject json = JObject.Parse(await result);
            string token = (string)json["id_token"];
            if (string.IsNullOrEmpty(token))
            {
                string error = (string)json["error"] ?? "sign_in_failed";
                throw new ApiException(0, error == "cancelled" ? "sign_in_cancelled" : "sign_in_failed", error);
            }
            return new ProviderToken(token, nonce);
        }

        private static string NewNonce()
        {
            var bytes = new byte[24];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string Sha256Hex(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    /// <summary>Play Install Referrer: carries the challenge code through a fresh Android install.</summary>
    public static class InstallReferrer
    {
        public static void Request()
        {
            if (!AndroidNative.IsDevice) return;
            using (AndroidJavaObject activity = AndroidNative.Activity())
                AndroidNative.CallStatic("InstallReferrerBridge", "fetch", activity);
        }
    }
}
