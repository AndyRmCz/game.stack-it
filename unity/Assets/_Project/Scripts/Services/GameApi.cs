using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackIt.Client;
using StackIt.Client.Api;
using StackIt.Core.Simulation;
using UnityEngine;

namespace StackIt.Services
{
    /// <summary>Typed endpoints of the backend, plus the run lifecycle (reserve, submit, offline queue).</summary>
    public sealed class GameApi
    {
        private readonly ApiClient _api;
        private readonly OfflineRunQueue _queue;
        private readonly SaveService _save;
        private Task<StartRunResponse> _reserved;
        private bool _flushing;

        public Profile Profile { get; private set; }
        public event Action<Profile> ProfileChanged;

        public GameApi(ApiClient api, OfflineRunQueue queue, SaveService save)
        {
            _api = api;
            _queue = queue;
            _save = save;
            if (save.CachedProfileJson != null)
            {
                try
                {
                    Profile = JsonConvert.DeserializeObject<Profile>(save.CachedProfileJson, ApiClient.Json);
                }
                catch (JsonException)
                {
                    save.CachedProfileJson = null;
                }
            }
        }

        public int PendingSubmissions => _queue.Count;

        // --- profile -----------------------------------------------------------------------------

        public async Task<Profile> RefreshProfileAsync()
        {
            SetProfile(await _api.Get<Profile>("/v1/me"));
            return Profile;
        }

        public async Task RenameAsync(string name) =>
            SetProfile(await _api.Patch<Profile>("/v1/me", new RenameRequest { DisplayName = name }));

        public Task DeleteAccountAsync() => _api.Delete("/v1/me");

        public void SetCoins(int balance)
        {
            if (Profile == null) return;
            Profile.Coins = balance;
            SetProfile(Profile);
        }

        private void SetProfile(Profile profile)
        {
            Profile = profile;
            _save.CachedProfileJson = JsonConvert.SerializeObject(profile, ApiClient.Json);
            ProfileChanged?.Invoke(profile);
        }

        // --- runs --------------------------------------------------------------------------------

        /// <summary>
        /// Seed for the next normal run. A run is reserved one game ahead so pressing Play never
        /// waits for the network.
        /// </summary>
        public async Task<StartRunResponse> TakeReservedRunAsync()
        {
            Task<StartRunResponse> reserved = _reserved ?? StartAsync(null);
            _reserved = null;
            try
            {
                return await reserved;
            }
            catch (ApiException)
            {
                return null; // offline: the caller plays an unranked local run
            }
        }

        public void ReserveNextRun()
        {
            _reserved = StartAsync(null);
            _reserved.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public Task<StartRunResponse> StartChallengeRunAsync(string code) => StartAsync(code);

        private Task<StartRunResponse> StartAsync(string challengeCode) =>
            _api.Post<StartRunResponse>("/v1/runs", new StartRunRequest { ChallengeCode = challengeCode });

        /// <summary>Submits a finished run; if offline, keeps it for later and returns null.</summary>
        public async Task<SubmitRunResponse> SubmitRunAsync(string runId, IReadOnlyList<InputEvent> events, int score)
        {
            var body = new SubmitRunRequest { ClaimedScore = score };
            foreach (InputEvent e in events) body.Events.Add(new RunEvent { T = e.Tick, Type = e.TypeName });
            string payload = JsonConvert.SerializeObject(body, ApiClient.Json);
            _queue.Enqueue(runId, payload); // persisted first: survives the app being killed

            try
            {
                SubmitRunResponse result = await _api.Post<SubmitRunResponse>($"/v1/runs/{runId}/submit", body);
                _queue.Remove(runId);
                ApplyRunResult(result);
                return result;
            }
            catch (ApiException e) when (e.IsOffline || e.Status >= 500)
            {
                return null;
            }
            catch (ApiException)
            {
                _queue.Remove(runId); // the server answered definitively (e.g. run expired)
                throw;
            }
        }

        /// <summary>Retries queued submissions. Safe to call often: the server's submit is idempotent.</summary>
        public async Task FlushQueueAsync()
        {
            if (_flushing) return;
            _flushing = true;
            try
            {
                while (_queue.Count > 0)
                {
                    PendingRun pending = _queue.Peek();
                    var body = JsonConvert.DeserializeObject<SubmitRunRequest>(pending.Payload, ApiClient.Json);
                    try
                    {
                        ApplyRunResult(await _api.Post<SubmitRunResponse>($"/v1/runs/{pending.RunId}/submit", body));
                        _queue.Remove(pending.RunId);
                    }
                    catch (ApiException e) when (e.IsOffline || e.Status >= 500)
                    {
                        return;
                    }
                    catch (ApiException e)
                    {
                        Debug.LogWarning($"Dropping queued run {pending.RunId}: {e.Code}");
                        _queue.Remove(pending.RunId);
                    }
                }
            }
            finally
            {
                _flushing = false;
            }
        }

        private void ApplyRunResult(SubmitRunResponse result)
        {
            if (result == null || Profile == null) return;
            Profile.BestScore = Math.Max(Profile.BestScore, result.BestScore);
            Profile.Coins = result.Balance;
            SetProfile(Profile);
        }

        public async Task<int> DoubleCoinsAsync(string runId)
        {
            BalanceResponse balance = await _api.Post<BalanceResponse>($"/v1/runs/{runId}/double-coins", null);
            SetCoins(balance.Balance);
            return balance.Balance;
        }

        // --- social ------------------------------------------------------------------------------

        public Task<ChallengeInfo> CreateChallengeAsync(string runId) =>
            _api.Post<ChallengeInfo>("/v1/challenges", new CreateChallengeRequest { RunId = runId });

        public Task<ChallengeInfo> GetChallengeAsync(string code) =>
            _api.Get<ChallengeInfo>("/v1/challenges/" + Uri.EscapeDataString(code));

        public Task<List<Friend>> FriendsAsync() => _api.Get<List<Friend>>("/v1/friends");

        public Task<Friend> AddFriendAsync(string code) =>
            _api.Post<Friend>("/v1/friends", new AddFriendRequest { FriendCode = code });

        public Task RemoveFriendAsync(string userId) => _api.Delete("/v1/friends/" + userId);

        public Task<Leaderboard> LeaderboardAsync(bool weekly) =>
            _api.Get<Leaderboard>("/v1/leaderboards/friends?period=" + (weekly ? "weekly" : "all_time"));

        // --- economy -----------------------------------------------------------------------------

        public Task<Shop> ShopAsync() => _api.Get<Shop>("/v1/shop");

        public async Task<Shop> PurchaseAsync(string skinId, string idempotencyKey)
        {
            Shop shop = await _api.Post<Shop>("/v1/shop/purchase", new SkinRequest { SkinId = skinId }, idempotencyKey: idempotencyKey);
            SetCoins(shop.Balance);
            return shop;
        }

        public async Task<Shop> EquipAsync(string skinId)
        {
            Shop shop = await _api.Post<Shop>("/v1/skins/equip", new SkinRequest { SkinId = skinId });
            await RefreshProfileAsync();
            return shop;
        }

        public Task<DailyStatus> DailyAsync() => _api.Get<DailyStatus>("/v1/daily");

        public async Task<DailyStatus> ClaimDailyAsync(string idempotencyKey)
        {
            DailyStatus status = await _api.Post<DailyStatus>("/v1/daily/claim", null, idempotencyKey: idempotencyKey);
            if (status.Balance.HasValue) SetCoins(status.Balance.Value);
            return status;
        }
    }
}
