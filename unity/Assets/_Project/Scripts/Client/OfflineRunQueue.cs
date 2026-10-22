using System;
using System.Collections.Generic;
using System.Text;

namespace StackIt.Client
{
    /// <summary>Platform key/value persistence (PlayerPrefs in the game, a dictionary in tests).</summary>
    public interface IKeyValueStore
    {
        string GetString(string key);
        void SetString(string key, string value);
        void Delete(string key);
    }

    public readonly struct PendingRun
    {
        public readonly string RunId;
        public readonly string Payload;

        public PendingRun(string runId, string payload)
        {
            RunId = runId;
            Payload = payload;
        }
    }

    /// <summary>
    /// Run submissions waiting for connectivity. Persisted so a run finished offline (or when the
    /// app is killed) is still verified later. One entry per run; the server's submit is
    /// idempotent, so replaying an entry is always safe.
    /// </summary>
    public sealed class OfflineRunQueue
    {
        private const string Key = "stackit.offline_runs.v1";
        private readonly IKeyValueStore _store;
        private readonly int _capacity;
        private readonly List<PendingRun> _items;

        public OfflineRunQueue(IKeyValueStore store, int capacity = 20)
        {
            _store = store;
            _capacity = capacity;
            _items = Decode(store.GetString(Key));
        }

        public int Count => _items.Count;

        public void Enqueue(string runId, string payload)
        {
            _items.RemoveAll(i => i.RunId == runId);
            _items.Add(new PendingRun(runId, payload));
            while (_items.Count > _capacity) _items.RemoveAt(0);
            Save();
        }

        public PendingRun Peek()
        {
            if (_items.Count == 0) throw new InvalidOperationException("queue is empty");
            return _items[0];
        }

        public void Remove(string runId)
        {
            if (_items.RemoveAll(i => i.RunId == runId) > 0) Save();
        }

        private void Save()
        {
            if (_items.Count == 0)
            {
                _store.Delete(Key);
                return;
            }
            // Base64 fields joined by newlines: no escaping rules to get wrong.
            var sb = new StringBuilder();
            foreach (PendingRun item in _items)
            {
                sb.Append(B64(item.RunId)).Append(' ').Append(B64(item.Payload)).Append('\n');
            }
            _store.SetString(Key, sb.ToString());
        }

        private static List<PendingRun> Decode(string raw)
        {
            var items = new List<PendingRun>();
            if (string.IsNullOrEmpty(raw)) return items;
            foreach (string line in raw.Split('\n'))
            {
                string[] parts = line.Split(' ');
                if (parts.Length != 2) continue;
                try
                {
                    items.Add(new PendingRun(FromB64(parts[0]), FromB64(parts[1])));
                }
                catch (FormatException)
                {
                    // Corrupted entry: drop it rather than block the whole queue.
                }
            }
            return items;
        }

        private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
        private static string FromB64(string s) => Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}
