using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace StackIt.Services.Native
{
    /// <summary>
    /// Receives UnitySendMessage callbacks from the iOS (.mm) and Android (Java) plugins and
    /// completes the pending task. Plugins always call back on "StackItNative".
    /// </summary>
    public sealed class NativeCallbacks : MonoBehaviour
    {
        public const string ObjectName = "StackItNative";

        private static NativeCallbacks _instance;
        private readonly Dictionary<string, TaskCompletionSource<string>> _pending =
            new Dictionary<string, TaskCompletionSource<string>>();

        public static event Action<string> InstallReferrerReceived;

        public static NativeCallbacks Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject(ObjectName);
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<NativeCallbacks>();
                }
                return _instance;
            }
        }

        /// <summary>Registers a one-shot wait for a callback channel ("signin", "export", ...).</summary>
        public Task<string> Expect(string channel)
        {
            if (_pending.TryGetValue(channel, out var previous)) previous.TrySetCanceled();
            var tcs = new TaskCompletionSource<string>();
            _pending[channel] = tcs;
            return tcs.Task;
        }

        private void Complete(string channel, string message)
        {
            if (_pending.TryGetValue(channel, out var tcs))
            {
                _pending.Remove(channel);
                tcs.TrySetResult(message);
            }
        }

        // Method names are part of the native contract: see Plugins/iOS/*.mm and Plugins/Android.
        public void OnSignInResult(string json) => Complete("signin", json);
        public void OnClipExported(string pathOrError) => Complete("export", pathOrError);
        public void OnInstallReferrer(string referrer) => InstallReferrerReceived?.Invoke(referrer);
    }

    /// <summary>P/Invoke surface of Plugins/iOS. Only callable on iOS devices.</summary>
    internal static class IosNative
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] public static extern void _StackItHaptic(int kind);
        [DllImport("__Internal")] public static extern void _StackItShare(string text, string url, string filePath);
        [DllImport("__Internal")] public static extern void _StackItShareInstagramStory(string facebookAppId, string text, string url, string filePath);
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] public static extern bool _StackItRecStart(int width, int height, int fps, int bitrate, int keepSeconds);
        [DllImport("__Internal")] public static extern void _StackItRecAppend(IntPtr bgra, int length, int bytesPerRow, double seconds);
        [DllImport("__Internal")] public static extern void _StackItRecExport(string path, double lastSeconds);
        [DllImport("__Internal")] public static extern void _StackItRecStop();
        [DllImport("__Internal")] public static extern void _StackItSignInApple(string nonce);
        [DllImport("__Internal")] public static extern void _StackItSignInGoogle(string clientId);
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] public static extern bool _StackItGoogleAvailable(); // C++ bool is 1 byte
#else
        public static void _StackItHaptic(int kind) { }
        public static void _StackItShare(string text, string url, string filePath) { }
        public static void _StackItShareInstagramStory(string facebookAppId, string text, string url, string filePath) { }
        public static bool _StackItRecStart(int width, int height, int fps, int bitrate, int keepSeconds) => false;
        public static void _StackItRecAppend(IntPtr bgra, int length, int bytesPerRow, double seconds) { }
        public static void _StackItRecExport(string path, double lastSeconds) { }
        public static void _StackItRecStop() { }
        public static void _StackItSignInApple(string nonce) { }
        public static void _StackItSignInGoogle(string clientId) { }
        public static bool _StackItGoogleAvailable() => false;
#endif
    }

    /// <summary>Helpers for the Java side (Plugins/Android/StackItNative.androidlib).</summary>
    internal static class AndroidNative
    {
        public const string Package = "com.stackit.nativeplugin.";

        public static bool IsDevice => Application.platform == RuntimePlatform.Android;

        public static AndroidJavaObject Activity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                return player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        public static void CallStatic(string cls, string method, params object[] args)
        {
            if (!IsDevice) return;
            using (var c = new AndroidJavaClass(Package + cls)) c.CallStatic(method, args);
        }

        public static T CallStatic<T>(string cls, string method, params object[] args)
        {
            if (!IsDevice) return default;
            using (var c = new AndroidJavaClass(Package + cls)) return c.CallStatic<T>(method, args);
        }
    }
}
