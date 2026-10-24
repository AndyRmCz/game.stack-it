using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StackIt.Client
{
    /// <summary>Event names: the list in shared/telemetry/events.json (the server rejects others).</summary>
    public static class TelemetryEvents
    {
        public const string SessionStart = "session_start";
        public const string SessionEnd = "session_end";
        public const string ScreenView = "screen_view";
        public const string RunStart = "run_start";
        public const string RunEnd = "run_end";
        public const string OfferShown = "offer_shown";
        public const string OfferAccepted = "offer_accepted";
        public const string OfferDeclined = "offer_declined";
        public const string AdRequested = "ad_requested";
        public const string AdLoaded = "ad_loaded";
        public const string AdLoadFailed = "ad_load_failed";
        public const string AdShown = "ad_shown";
        public const string AdShowFailed = "ad_show_failed";
        public const string AdClicked = "ad_clicked";
        public const string AdRewarded = "ad_rewarded";
        public const string AdClosed = "ad_closed";
        public const string AdPaid = "ad_paid";
        public const string ShopView = "shop_view";
        public const string ItemPreview = "item_preview";
        public const string ItemUnaffordableTap = "item_unaffordable_tap";
        public const string MusicPreview = "music_preview";
        public const string MissionClaimed = "mission_claimed";
        public const string ShareTap = "share_tap";
        public const string ClipExported = "clip_exported";
        public const string ChallengeOpened = "challenge_opened";
        public const string SettingChanged = "setting_changed";
        public const string PerfSample = "perf_sample";
        public const string ApiError = "api_error";

        public static readonly string[] All =
        {
            SessionStart, SessionEnd, ScreenView, RunStart, RunEnd, OfferShown, OfferAccepted, OfferDeclined,
            AdRequested, AdLoaded, AdLoadFailed, AdShown, AdShowFailed, AdClicked, AdRewarded, AdClosed, AdPaid,
            ShopView, ItemPreview, ItemUnaffordableTap, MusicPreview, MissionClaimed, ShareTap, ClipExported, ChallengeOpened, SettingChanged,
            PerfSample, ApiError,
        };
    }

    public static class AdFormats
    {
        public const string Interstitial = "interstitial";
        public const string Rewarded = "rewarded";
        public const string Banner = "banner";
    }

    /// <summary>Where an ad or a rewarded offer appears.</summary>
    public static class AdPlacements
    {
        public const string Continue = "continue";
        public const string DoubleCoins = "double";
        public const string BetweenRuns = "between_runs";
        public const string Menu = "menu";
        public const string MusicTrial = "music_trial";
    }

    /// <summary>Limits shared with app/domain/telemetry.py.</summary>
    public static class TelemetryText
    {
        public const int MaxBatch = 100;
        public const int MaxString = 64;

        private static readonly HashSet<string> PathWords = new HashSet<string>
        {
            "submit", "double-coins", "purchase", "equip", "claim", "friends", "guest", "refresh", "logout",
            "google", "apple", "link", "login", "me", "runs", "challenges", "leaderboards", "shop", "skins",
            "daily", "wallet", "events", "v1", "auth",
        };

        /// <summary>Cuts strings to what the server accepts (an over-long prop rejects the whole event).</summary>
        public static string Clamp(string value) =>
            value == null || value.Length <= MaxString ? value : value.Substring(0, MaxString);

        /// <summary>"/v1/runs/3f…/submit?x=1" → "/v1/runs/{id}/submit": no ids or codes in telemetry.</summary>
        public static string PathTemplate(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            int query = path.IndexOf('?');
            if (query >= 0) path = path.Substring(0, query);
            string[] parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0 && !PathWords.Contains(parts[i])) parts[i] = "{id}";
            }
            return Clamp(string.Join("/", parts));
        }
    }

    public readonly struct QueuedEvent
    {
        public readonly string Id;
        public readonly string Json;

        public QueuedEvent(string id, string json)
        {
            Id = id;
            Json = json;
        }
    }

    /// <summary>
    /// Serialized events waiting to be sent, persisted so nothing is lost offline or when the app
    /// is killed. Bounded: past the capacity the oldest events are dropped (the server keeps the
    /// ids it has, so re-sending after a crash is harmless).
    /// </summary>
    public sealed class TelemetryQueue
    {
        private const string Key = "stackit.telemetry_queue.v1";
        private readonly IKeyValueStore _store;
        private readonly int _capacity;
        private readonly List<QueuedEvent> _items;

        public TelemetryQueue(IKeyValueStore store, int capacity = 500)
        {
            _store = store;
            _capacity = capacity;
            _items = Decode(store.GetString(Key));
        }

        public int Count => _items.Count;

        public void Enqueue(string id, string json)
        {
            _items.Add(new QueuedEvent(id, json));
            while (_items.Count > _capacity) _items.RemoveAt(0);
            Save();
        }

        /// <summary>The oldest events, at most <paramref name="max"/>. They stay queued until removed.</summary>
        public List<QueuedEvent> Peek(int max = TelemetryText.MaxBatch) =>
            _items.GetRange(0, Math.Min(max, _items.Count));

        public void Remove(IEnumerable<string> ids)
        {
            var set = new HashSet<string>(ids);
            if (_items.RemoveAll(i => set.Contains(i.Id)) > 0) Save();
        }

        private void Save()
        {
            if (_items.Count == 0)
            {
                _store.Delete(Key);
                return;
            }
            var sb = new StringBuilder();
            foreach (QueuedEvent item in _items)
                sb.Append(B64(item.Id)).Append(' ').Append(B64(item.Json)).Append('\n');
            _store.SetString(Key, sb.ToString());
        }

        private static List<QueuedEvent> Decode(string raw)
        {
            var items = new List<QueuedEvent>();
            if (string.IsNullOrEmpty(raw)) return items;
            foreach (string line in raw.Split('\n'))
            {
                string[] parts = line.Split(' ');
                if (parts.Length != 2) continue;
                try
                {
                    items.Add(new QueuedEvent(FromB64(parts[0]), FromB64(parts[1])));
                }
                catch (FormatException)
                {
                    // Corrupted entry: drop it rather than block the queue.
                }
            }
            return items;
        }

        private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
        private static string FromB64(string s) => Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }

    /// <summary>A session that is over, reported as session_end.</summary>
    public sealed class EndedSession
    {
        public string Id;
        public int Index;
        public double DurationSeconds;
        public int Runs;
        public int Ads;

        /// <summary>"timeout": back after more than the resume window; "relaunch": the app was killed.</summary>
        public string EndedBy;
    }

    public readonly struct SessionChange
    {
        /// <summary>The previous session, when this call ended it (else null).</summary>
        public readonly EndedSession Ended;

        /// <summary>True when a new session started (false: the current one resumed).</summary>
        public readonly bool Started;

        public SessionChange(EndedSession ended, bool started)
        {
            Ended = ended;
            Started = started;
        }
    }

    /// <summary>
    /// App sessions (docs/PLAN.md, "Measurement findings" ▸ I): a session runs from foreground to
    /// background; coming back within <see cref="ResumeWindowSeconds"/> continues it. Length counts
    /// foreground time only. State is persisted so a session cut short by a kill is still reported
    /// (as ended by "relaunch") on the next launch.
    /// </summary>
    public sealed class SessionTracker
    {
        public const double ResumeWindowSeconds = 30;
        private const string Key = "stackit.telemetry_session.v1";

        private readonly IKeyValueStore _store;
        private readonly Func<string> _newId;
        private bool _open;
        private bool _inForeground;
        private DateTime _foregroundSince;
        private DateTime _backgroundSince;
        private double _foregroundSeconds;

        public string SessionId { get; private set; }
        public int Index { get; private set; }
        public int Runs { get; private set; }
        public int Ads { get; private set; }

        public SessionTracker(IKeyValueStore store, Func<string> newId)
        {
            _store = store;
            _newId = newId;
            Load();
        }

        /// <summary>Launch (<paramref name="coldStart"/>) or return to the app.</summary>
        public SessionChange Foreground(DateTime nowUtc, bool coldStart)
        {
            if (_inForeground && !coldStart) return new SessionChange(null, false);
            EndedSession ended = null;
            if (_open)
            {
                bool resumable = !coldStart && (nowUtc - _backgroundSince).TotalSeconds <= ResumeWindowSeconds;
                if (resumable)
                {
                    _inForeground = true;
                    _foregroundSince = nowUtc;
                    Save();
                    return new SessionChange(null, false);
                }
                ended = new EndedSession
                {
                    Id = SessionId,
                    Index = Index,
                    DurationSeconds = Math.Round(_foregroundSeconds, 1),
                    Runs = Runs,
                    Ads = Ads,
                    EndedBy = coldStart ? "relaunch" : "timeout",
                };
            }
            SessionId = _newId();
            Index++;
            Runs = 0;
            Ads = 0;
            _foregroundSeconds = 0;
            _open = true;
            _inForeground = true;
            _foregroundSince = nowUtc;
            Save();
            return new SessionChange(ended, true);
        }

        public void Background(DateTime nowUtc)
        {
            if (!_inForeground) return;
            Accumulate(nowUtc);
            _inForeground = false;
            _backgroundSince = nowUtc;
            Save();
        }

        /// <summary>Banks the foreground time so far (call now and then: a kill loses nothing older).</summary>
        public void Checkpoint(DateTime nowUtc)
        {
            if (!_inForeground) return;
            Accumulate(nowUtc);
            Save();
        }

        public double ForegroundSeconds(DateTime nowUtc) =>
            _foregroundSeconds + (_inForeground ? Math.Max(0, (nowUtc - _foregroundSince).TotalSeconds) : 0);

        public void CountRun()
        {
            Runs++;
            Save();
        }

        public void CountAd()
        {
            Ads++;
            Save();
        }

        private void Accumulate(DateTime nowUtc)
        {
            _foregroundSeconds += Math.Max(0, (nowUtc - _foregroundSince).TotalSeconds);
            _foregroundSince = nowUtc;
        }

        private void Save()
        {
            string fields = string.Join("|",
                SessionId ?? "",
                Index.ToString(CultureInfo.InvariantCulture),
                Runs.ToString(CultureInfo.InvariantCulture),
                Ads.ToString(CultureInfo.InvariantCulture),
                _foregroundSeconds.ToString("R", CultureInfo.InvariantCulture),
                _backgroundSince.Ticks.ToString(CultureInfo.InvariantCulture),
                _open ? "1" : "0");
            _store.SetString(Key, fields);
        }

        private void Load()
        {
            string raw = _store.GetString(Key);
            if (string.IsNullOrEmpty(raw)) return;
            string[] f = raw.Split('|');
            if (f.Length != 7) return;
            try
            {
                SessionId = f[0].Length > 0 ? f[0] : null;
                Index = int.Parse(f[1], CultureInfo.InvariantCulture);
                Runs = int.Parse(f[2], CultureInfo.InvariantCulture);
                Ads = int.Parse(f[3], CultureInfo.InvariantCulture);
                _foregroundSeconds = double.Parse(f[4], CultureInfo.InvariantCulture);
                _backgroundSince = new DateTime(long.Parse(f[5], CultureInfo.InvariantCulture), DateTimeKind.Utc);
                _open = f[6] == "1" && SessionId != null;
            }
            catch (FormatException)
            {
                _open = false;
            }
        }
    }

    /// <summary>Frame times of one session, reduced to the frame rate a player actually saw.</summary>
    public sealed class FrameStats
    {
        private readonly List<float> _frames = new List<float>();
        private readonly int _capacity;
        private int _next;

        public FrameStats(int capacity = 18000)
        {
            _capacity = capacity;
        }

        public int Count => _frames.Count;

        public void Add(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            if (_frames.Count < _capacity) _frames.Add(deltaSeconds);
            else _frames[_next] = deltaSeconds;
            _next = (_next + 1) % _capacity;
        }

        public void Clear()
        {
            _frames.Clear();
            _next = 0;
        }

        /// <summary>Median frame rate.</summary>
        public float MedianFps => Fps(0.5f);

        /// <summary>The frame rate of the slowest 5 % of frames (stutter).</summary>
        public float P5Fps => Fps(0.95f);

        private float Fps(float frameTimeQuantile)
        {
            if (_frames.Count == 0) return 0f;
            var sorted = new List<float>(_frames);
            sorted.Sort();
            int i = Math.Min(sorted.Count - 1, (int)(frameTimeQuantile * sorted.Count));
            return (float)Math.Round(1f / sorted[i], 1);
        }
    }
}
