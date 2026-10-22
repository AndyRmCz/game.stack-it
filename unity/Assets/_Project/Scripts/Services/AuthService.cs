using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using StackIt.Client;
using StackIt.Client.Api;
using UnityEngine;

namespace StackIt.Services
{
    /// <summary>
    /// Session management: instant guest account bound to this install (device ID + random
    /// secret), optional Google/Apple linking, rotating refresh tokens.
    /// </summary>
    public sealed class AuthService : ITokenProvider
    {
        private const string DeviceIdKey = "stackit.device_id";
        private const string DeviceSecretKey = "stackit.device_secret";
        private const string RefreshKey = "stackit.refresh_token";

        private readonly ApiClient _api;
        private readonly IKeyValueStore _store;
        private readonly ISignInService _signIn;
        private Task<bool> _renewing;

        public string AccessToken { get; private set; }
        public string UserId { get; private set; }
        public bool IsSignedIn => AccessToken != null;

        public AuthService(ApiClient api, IKeyValueStore store, ISignInService signIn)
        {
            _api = api;
            _store = store;
            _signIn = signIn;
            _api.Tokens = this;
        }

        /// <summary>Resumes the session or creates/recovers the device's guest account.</summary>
        public async Task EnsureSignedInAsync()
        {
            if (IsSignedIn) return;
            if (await TryRefreshAsync()) return;
            await GuestAsync();
        }

        public Task<bool> RenewAsync()
        {
            // Concurrent 401s share one refresh: a rotated token must never be used twice.
            if (_renewing == null || _renewing.IsCompleted) _renewing = RenewOnceAsync();
            return _renewing;
        }

        private async Task<bool> RenewOnceAsync()
        {
            AccessToken = null;
            if (await TryRefreshAsync()) return true;
            try
            {
                await GuestAsync();
                return true;
            }
            catch (ApiException)
            {
                return false;
            }
        }

        public bool CanUse(SignInProvider provider) => _signIn.IsAvailable(provider);

        /// <summary>Links Google/Apple to the current account so progress survives a new phone.</summary>
        public async Task LinkAsync(SignInProvider provider)
        {
            ProviderToken token = await _signIn.SignInAsync(provider);
            await _api.Post<object>($"/v1/auth/{Name(provider)}/link", Body(token));
        }

        /// <summary>Signs in to an existing Google/Apple account (e.g. after reinstalling).</summary>
        public async Task SwitchToProviderAccountAsync(SignInProvider provider)
        {
            ProviderToken token = await _signIn.SignInAsync(provider);
            Apply(await _api.Post<TokenResponse>($"/v1/auth/{Name(provider)}/login", Body(token), auth: false));
        }

        public async Task SignOutLocallyAsync()
        {
            string refresh = _store.GetString(RefreshKey);
            if (refresh != null)
            {
                try
                {
                    await _api.Post<object>("/v1/auth/logout", new RefreshRequest { RefreshToken = refresh }, auth: false);
                }
                catch (ApiException)
                {
                    // Best effort: the token expires on its own.
                }
            }
            _store.Delete(RefreshKey);
            AccessToken = null;
            UserId = null;
        }

        /// <summary>After account deletion: forget everything so the next launch starts fresh.</summary>
        public void ForgetDevice()
        {
            _store.Delete(RefreshKey);
            _store.Delete(DeviceIdKey);
            _store.Delete(DeviceSecretKey);
            AccessToken = null;
            UserId = null;
        }

        private async Task<bool> TryRefreshAsync()
        {
            string refresh = _store.GetString(RefreshKey);
            if (refresh == null) return false;
            try
            {
                Apply(await _api.Post<TokenResponse>("/v1/auth/refresh", new RefreshRequest { RefreshToken = refresh }, auth: false));
                return true;
            }
            catch (ApiException e) when (!e.IsOffline)
            {
                _store.Delete(RefreshKey);
                return false;
            }
        }

        private async Task GuestAsync()
        {
            var body = new GuestAuthRequest { DeviceId = DeviceId(), DeviceSecret = DeviceSecret() };
            Apply(await _api.Post<TokenResponse>("/v1/auth/guest", body, auth: false));
        }

        private void Apply(TokenResponse tokens)
        {
            AccessToken = tokens.AccessToken;
            UserId = tokens.UserId;
            _store.SetString(RefreshKey, tokens.RefreshToken);
        }

        private string DeviceId()
        {
            string id = _store.GetString(DeviceIdKey);
            if (id != null) return id;
            // SystemInfo.deviceUniqueIdentifier changes on reinstall on iOS; a random ID is as good
            // and leaks nothing about the hardware.
            id = "dev-" + Guid.NewGuid().ToString("N");
            _store.SetString(DeviceIdKey, id);
            return id;
        }

        private string DeviceSecret()
        {
            string secret = _store.GetString(DeviceSecretKey);
            if (secret != null) return secret;
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            secret = Convert.ToBase64String(bytes);
            _store.SetString(DeviceSecretKey, secret);
            return secret;
        }

        private static ProviderTokenRequest Body(ProviderToken token) =>
            new ProviderTokenRequest { IdToken = token.IdToken, Nonce = token.Nonce };

        private static string Name(SignInProvider provider) => provider == SignInProvider.Google ? "google" : "apple";
    }
}
