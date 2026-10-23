using System;
using System.Collections.Generic;
using System.Globalization;

namespace StackIt.Client
{
    /// <summary>A challenge the player opened (from a link or a typed code) but has not played yet.</summary>
    public readonly struct PendingChallenge
    {
        public readonly string Code;
        public readonly string CreatorName;
        public readonly int Score;

        public PendingChallenge(string code, string creatorName, int score)
        {
            Code = code;
            CreatorName = creatorName;
            Score = score;
        }
    }

    /// <summary>
    /// Challenges waiting for the player, kept on the device (the server has no inbox): shown at the
    /// top of Friends and as the dot on its nav item. Newest first, at most <see cref="Max"/>.
    /// </summary>
    public sealed class PendingChallenges
    {
        public const string StoreKey = "stackit.pending_challenges";
        public const int Max = 10;

        private readonly IKeyValueStore _store;
        private readonly List<PendingChallenge> _items = new List<PendingChallenge>();

        public PendingChallenges(IKeyValueStore store)
        {
            _store = store;
            string saved = store.GetString(StoreKey);
            if (string.IsNullOrEmpty(saved)) return;
            foreach (string line in saved.Split('\n'))
            {
                string[] parts = line.Split('\t');
                if (parts.Length != 3 || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int score)) continue;
                _items.Add(new PendingChallenge(parts[0], parts[1], score));
            }
        }

        public IReadOnlyList<PendingChallenge> Items => _items;

        public bool Any => _items.Count > 0;

        public event Action Changed;

        public void Add(string code, string creatorName, int score)
        {
            _items.RemoveAll(c => c.Code == code);
            _items.Insert(0, new PendingChallenge(code, Clean(creatorName), score));
            if (_items.Count > Max) _items.RemoveRange(Max, _items.Count - Max);
            Save();
        }

        /// <summary>Played (or dismissed): no longer pending.</summary>
        public void Remove(string code)
        {
            if (_items.RemoveAll(c => c.Code == code) > 0) Save();
        }

        private void Save()
        {
            var lines = new List<string>();
            foreach (PendingChallenge c in _items)
                lines.Add(c.Code + "\t" + c.CreatorName + "\t" + c.Score.ToString(CultureInfo.InvariantCulture));
            _store.SetString(StoreKey, string.Join("\n", lines));
            Changed?.Invoke();
        }

        private static string Clean(string name) => (name ?? "").Replace('\t', ' ').Replace('\n', ' ');
    }

    /// <summary>
    /// "A new skin is affordable" for the Shop dot: an unowned item the balance covers that the
    /// player has not seen as affordable in the shop yet.
    /// </summary>
    public sealed class SeenSkins
    {
        public const string StoreKey = "stackit.seen_affordable";

        private readonly IKeyValueStore _store;
        private readonly HashSet<string> _seen;

        public SeenSkins(IKeyValueStore store)
        {
            _store = store;
            string saved = store.GetString(StoreKey);
            _seen = new HashSet<string>(string.IsNullOrEmpty(saved) ? Array.Empty<string>() : saved.Split(','));
        }

        public static IEnumerable<string> Affordable(IEnumerable<(string Id, int Price, bool Owned)> items, int balance)
        {
            foreach ((string id, int price, bool owned) in items)
                if (!owned && price <= balance) yield return id;
        }

        public bool HasNew(IEnumerable<(string Id, int Price, bool Owned)> items, int balance)
        {
            foreach (string id in Affordable(items, balance))
                if (!_seen.Contains(id)) return true;
            return false;
        }

        /// <summary>The shop was opened: everything affordable now has been seen.</summary>
        public void MarkSeen(IEnumerable<(string Id, int Price, bool Owned)> items, int balance)
        {
            bool changed = false;
            foreach (string id in Affordable(items, balance)) changed |= _seen.Add(id);
            if (changed) _store.SetString(StoreKey, string.Join(",", _seen));
        }
    }

    /// <summary>Weekly leaderboard reset: a week after the period started.</summary>
    public static class WeeklyReset
    {
        public static readonly TimeSpan Week = TimeSpan.FromDays(7);

        public static TimeSpan Remaining(DateTime sinceUtc, DateTime nowUtc)
        {
            TimeSpan left = sinceUtc + Week - nowUtc;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        /// <summary>Days and hours ("3d 4h"), or hours and minutes in the last day ("5h 12m").</summary>
        public static string Short(TimeSpan left, string dayUnit = "d", string hourUnit = "h", string minuteUnit = "m") =>
            left.TotalDays >= 1
                ? $"{(int)left.TotalDays}{dayUnit} {left.Hours}{hourUnit}"
                : $"{(int)left.TotalHours}{hourUnit} {left.Minutes}{minuteUnit}";
    }
}
