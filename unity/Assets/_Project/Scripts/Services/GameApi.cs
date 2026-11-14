using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private readonly RunReservation _reserved;
        private bool _flushing;

        // The last answer of each screen's read, drawn at once when the screen opens again.
        private readonly LastAnswer<Shop> _shop = new LastAnswer<Shop>();
        private readonly LastAnswer<MissionBoard> _missions = new LastAnswer<MissionBoard>();
        private readonly LastAnswer<DailyStatus> _daily = new LastAnswer<DailyStatus>();
        private readonly Dictionary<string, LastAnswer<Leaderboard>> _boards = new Dictionary<string, LastAnswer<Leaderboard>>();
        private readonly LastAnswer<List<Friend>> _friends = new LastAnswer<List<Friend>>();

        public Profile Profile { get; private set; }
        public event Action<Profile> ProfileChanged;

        public GameApi(ApiClient api, OfflineRunQueue queue, SaveService save)
        {
            _api = api;
            _queue = queue;
            _save = save;
            _reserved = new RunReservation(() => StartAsync(null));
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
            if (Profile != null && profile != null && Profile.UserId != profile.UserId) ForgetLastAnswers();
            if (profile != null && _shop.Value != null) _shop.Value.Balance = profile.Coins; // affordable or not
            Profile = profile;
            _save.CachedProfileJson = JsonConvert.SerializeObject(profile, ApiClient.Json);
            ProfileChanged?.Invoke(profile);
        }

        // --- runs --------------------------------------------------------------------------------

        /// <summary>
        /// Seed for the next normal run. A run is reserved one game ahead so pressing Play never
        /// waits for the network; without one, Play waits at most a moment (RunReservation).
        /// </summary>
        public async Task<StartRunResponse> TakeReservedRunAsync()
        {
            try
            {
                return await _reserved.TakeAsync();
            }
            catch (ApiException)
            {
                return null; // offline: the caller plays an unranked local run
            }
        }

        public void ReserveNextRun() => _reserved.Reserve();

        public Task<StartRunResponse> StartChallengeRunAsync(string code) => StartAsync(code);

        /// <summary>A run on today's Daily Tower: the same seed for every player.</summary>
        public Task<StartRunResponse> StartDailyRunAsync() =>
            _api.Post<StartRunResponse>("/v1/runs", new StartRunRequest { Daily = true });

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

        // --- telemetry ---------------------------------------------------------------------------

        /// <summary>Sends events already serialized as JSON objects (TelemetryService builds them).</summary>
        public Task<TelemetryBatchResponse> SendEventsAsync(string appVersion, string platform, IEnumerable<string> events)
        {
            var body = new TelemetryBatchBody { AppVersion = appVersion, Platform = platform, Events = new List<JRaw>() };
            foreach (string json in events) body.Events.Add(new JRaw(json));
            return _api.Post<TelemetryBatchResponse>("/v1/events", body);
        }

        private sealed class TelemetryBatchBody
        {
            public string AppVersion;
            public string Platform;
            public List<JRaw> Events;
        }

        public Task<List<Friend>> FriendsAsync() => Read(_friends, "/v1/friends");

        public async Task<Friend> AddFriendAsync(string code)
        {
            Friend friend = await _api.Post<Friend>("/v1/friends", new AddFriendRequest { FriendCode = code });
            List<Friend> kept = _friends.Value;
            if (kept != null && !kept.Exists(f => f.UserId == friend.UserId)) kept.Add(friend); // shown until the fresh list
            FriendsChanged();
            return friend;
        }

        public async Task RemoveFriendAsync(string userId)
        {
            await _api.Delete("/v1/friends/" + userId);
            _friends.Value?.RemoveAll(f => f.UserId == userId);
            FriendsChanged();
        }

        /// <summary>Lists and ranks read before a friend came or went are out of date.</summary>
        private void FriendsChanged()
        {
            _friends.Changed();
            foreach (LastAnswer<Leaderboard> board in _boards.Values) board.Changed();
        }

        /// <param name="period">"weekly", "all_time" or "daily" (today's Daily Tower).</param>
        public Task<Leaderboard> LeaderboardAsync(string period) =>
            Read(Board(period), "/v1/leaderboards/friends?period=" + period);

        // --- last answers ------------------------------------------------------------------------

        /// <summary>What the shop, missions, daily reward, ranks and friends said last time (null before
        /// the first answer), for their screens to draw while the fresh answer is on its way. Home's dots
        /// fill the first three, and signing in the friends.</summary>
        public Shop LastShop => _shop.Value;
        public MissionBoard LastMissions => _missions.Value;
        public DailyStatus LastDaily => _daily.Value;
        public Leaderboard LastLeaderboard(string period) => Board(period).Value;
        public List<Friend> LastFriends => _friends.Value;

        private LastAnswer<Leaderboard> Board(string period)
        {
            if (!_boards.TryGetValue(period, out LastAnswer<Leaderboard> board))
                _boards[period] = board = new LastAnswer<Leaderboard>();
            return board;
        }

        /// <summary>
        /// Reads <paramref name="path"/> and keeps the answer. An answer equal to the last one comes back
        /// as that same object, so a screen can skip drawing it twice.
        /// </summary>
        private async Task<T> Read<T>(LastAnswer<T> last, string path) where T : class
        {
            int ticket = last.Begin();
            T answer = await _api.Get<T>(path);
            T previous = last.Value;
            if (previous != null && JsonConvert.SerializeObject(previous, ApiClient.Json) == JsonConvert.SerializeObject(answer, ApiClient.Json))
                answer = previous;
            return last.Offer(ticket, answer);
        }

        private void ForgetLastAnswers()
        {
            _shop.Clear();
            _missions.Clear();
            _daily.Clear();
            foreach (LastAnswer<Leaderboard> board in _boards.Values) board.Clear();
            _friends.Clear();
        }

        // --- economy -----------------------------------------------------------------------------

        public Task<Shop> ShopAsync() => Read(_shop, "/v1/shop");

        public async Task<Shop> PurchaseAsync(string skinId, string idempotencyKey)
        {
            Shop shop = await _api.Post<Shop>("/v1/shop/purchase", new SkinRequest { SkinId = skinId }, idempotencyKey: idempotencyKey);
            _shop.Set(shop);
            SetCoins(shop.Balance);
            return shop;
        }

        public async Task<Shop> EquipAsync(string skinId)
        {
            Shop shop = await _api.Post<Shop>("/v1/skins/equip", new SkinRequest { SkinId = skinId });
            _shop.Set(shop);
            await RefreshProfileAsync();
            return shop;
        }

        public Task<MissionBoard> MissionsAsync() => Read(_missions, "/v1/missions");

        /// <summary>Claims a finished mission (slot 0-2) or the chest (3). Claiming twice pays once.</summary>
        public async Task<MissionBoard> ClaimMissionAsync(int slot)
        {
            MissionBoard missions = await _api.Post<MissionBoard>("/v1/missions/claim", new ClaimMissionRequest { Slot = slot });
            _missions.Set(missions);
            if (missions.Balance.HasValue) SetCoins(missions.Balance.Value);
            return missions;
        }

        public Task<DailyStatus> DailyAsync() => Read(_daily, "/v1/daily");

        public async Task<DailyStatus> ClaimDailyAsync(string idempotencyKey)
        {
            DailyStatus status = await _api.Post<DailyStatus>("/v1/daily/claim", null, idempotencyKey: idempotencyKey);
            _daily.Set(status);
            if (status.Balance.HasValue) SetCoins(status.Balance.Value);
            return status;
        }
    }
}
