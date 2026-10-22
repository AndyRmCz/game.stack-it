using System;
using StackIt.Client;
using StackIt.Services.Native;
using UnityEngine;

namespace StackIt.Services
{
    /// <summary>
    /// Turns every way a challenge can reach the app into one event: Universal Links / App Links
    /// (https://{base}/c/{code}), the custom scheme, the Play Install Referrer after a fresh
    /// install, and a code typed by hand.
    /// </summary>
    public sealed class DeepLinkService : IDisposable
    {
        private readonly string _baseUrl;
        private readonly string _scheme;

        public event Action<string> ChallengeReceived;

        /// <summary>Code that arrived before anyone was listening (cold start).</summary>
        public string PendingCode { get; private set; }

        public DeepLinkService(string baseUrl, string scheme)
        {
            _baseUrl = baseUrl;
            _scheme = scheme;
            Application.deepLinkActivated += OnUrl;
            NativeCallbacks.InstallReferrerReceived += OnReferrer;
        }

        public void Start(bool firstLaunch)
        {
            if (!string.IsNullOrEmpty(Application.absoluteURL)) OnUrl(Application.absoluteURL);
            if (firstLaunch) InstallReferrer.Request();
        }

        public bool SubmitTypedCode(string typed)
        {
            string code = DeepLinks.NormalizeTypedCode(typed);
            if (code == null) return false;
            Raise(code);
            return true;
        }

        public string TakePendingCode()
        {
            string code = PendingCode;
            PendingCode = null;
            return code;
        }

        private void OnUrl(string url)
        {
            string code = DeepLinks.ParseChallengeCode(url, _baseUrl, _scheme);
            if (code != null) Raise(code);
        }

        private void OnReferrer(string referrer)
        {
            string code = DeepLinks.ParseInstallReferrer(referrer);
            if (code != null) Raise(code);
        }

        private void Raise(string code)
        {
            if (ChallengeReceived == null) PendingCode = code;
            else ChallengeReceived.Invoke(code);
        }

        public void Dispose()
        {
            Application.deepLinkActivated -= OnUrl;
            NativeCallbacks.InstallReferrerReceived -= OnReferrer;
        }
    }
}
