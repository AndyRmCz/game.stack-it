using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using StackIt.Client;
using StackIt.Client.Api;
using UnityEngine;

namespace StackIt.Services
{
    /// <summary>
    /// Product telemetry to our own backend (POST /v1/events; docs/PLAN.md, "Measurement findings").
    /// Events are serialized when tracked, queued on disk and sent in batches every 30 s, when the
    /// app goes to the background, and once signed in. Sessions follow <see cref="SessionTracker"/>;
    /// pauses caused by a full-screen ad don't count as leaving the app.
    /// </summary>
    public sealed class TelemetryService : MonoBehaviour, ITelemetry
    {
        private const float FlushSeconds = 30f;
        private const int MinPerfFrames = 120;

        private GameApi _api;
        private Func<bool> _canSend;
        private Func<(string Key, object Value)[]> _sessionProps;
        private TelemetryQueue _queue;
        private SessionTracker _session;
        private readonly FrameStats _frames = new FrameStats();
        private float _frameSeconds;
        private float _nextFlush;
        private bool _flushing;
        private bool _startPending;
        private string _platform;

        /// <summary>True while a full-screen ad is up: the app pause it causes is not "leaving".</summary>
        public bool InFullScreenAd { get; set; }

        /// <param name="canSend">False until signed in: events wait in the queue.</param>
        /// <param name="sessionProps">Extra session_start props (OS, device, language, look).</param>
        public void Init(GameApi api, IKeyValueStore store, Func<bool> canSend, Func<(string Key, object Value)[]> sessionProps)
        {
            _api = api;
            _canSend = canSend;
            _sessionProps = sessionProps;
            _platform = Application.isEditor ? "editor"
                : Application.platform == RuntimePlatform.Android ? "android"
                : Application.platform == RuntimePlatform.IPhonePlayer ? "ios" : "other";
            _queue = new TelemetryQueue(store);
            _session = new SessionTracker(store, () => Guid.NewGuid().ToString());
            // The cold start's session_start waits for MarkLaunched (it carries the launch time).
            ReportEnded(_session.Foreground(DateTime.UtcNow, coldStart: true).Ended);
            _startPending = true;
            _nextFlush = Time.unscaledTime + FlushSeconds;
        }

        /// <summary>Home is on screen: reports the cold start with how long it took.</summary>
        public void MarkLaunched(float secondsSinceStartup)
        {
            if (!_startPending) return;
            _startPending = false;
            TrackSessionStart(coldStart: true, launchMs: Mathf.RoundToInt(secondsSinceStartup * 1000f));
            Flush();
        }

        public void Track(string name, params (string Key, object Value)[] props)
        {
            if (_session == null) return;
            Enqueue(name, _session.SessionId, props);
        }

        public void RunStarted(string runId, bool duel)
        {
            _session.CountRun();
            Track(TelemetryEvents.RunStart, ("run_id", runId), ("duel", duel), ("offline", runId == null));
        }

        public void OnAdEvent(AdEvent e)
        {
            switch (e.Name)
            {
                case TelemetryEvents.AdRequested:
                    Track(e.Name, ("format", e.Format));
                    break;
                case TelemetryEvents.AdLoaded:
                    Track(e.Name, ("format", e.Format), ("latency_ms", e.LatencyMs));
                    break;
                case TelemetryEvents.AdLoadFailed:
                    Track(e.Name, ("format", e.Format), ("error_code", e.ErrorCode));
                    break;
                case TelemetryEvents.AdShowFailed:
                    Track(e.Name, ("format", e.Format), ("placement", e.Placement), ("error_code", e.ErrorCode));
                    break;
                case TelemetryEvents.AdPaid:
                    Track(e.Name, ("format", e.Format), ("placement", e.Placement), ("value_micros", e.ValueMicros),
                        ("currency", e.Currency), ("precision", e.Precision), ("ad_source", e.Source));
                    break;
                default: // shown, clicked, rewarded, closed
                    if (e.Name == TelemetryEvents.AdShown) _session.CountAd();
                    Track(e.Name, ("format", e.Format), ("placement", e.Placement));
                    break;
            }
        }

        private void Update()
        {
            if (_session == null) return;
            float dt = Time.unscaledDeltaTime;
            _frames.Add(dt);
            _frameSeconds += dt;
            if (Time.unscaledTime < _nextFlush) return;
            _nextFlush = Time.unscaledTime + FlushSeconds;
            _session.Checkpoint(DateTime.UtcNow);
            Flush();
        }

        private void OnApplicationPause(bool paused)
        {
            if (_session == null || InFullScreenAd) return;
            if (paused)
            {
                ReportPerf();
                _session.Background(DateTime.UtcNow);
                Flush();
                return;
            }
            SessionChange change = _session.Foreground(DateTime.UtcNow, coldStart: false);
            ReportEnded(change.Ended);
            if (change.Started) TrackSessionStart(coldStart: false, launchMs: null);
        }

        private void TrackSessionStart(bool coldStart, int? launchMs)
        {
            var props = new List<(string Key, object Value)>
            {
                ("session_index", _session.Index),
                ("first_session", _session.Index == 1),
                ("cold_start", coldStart),
                ("launch_ms", launchMs),
            };
            if (_sessionProps != null) props.AddRange(_sessionProps());
            Track(TelemetryEvents.SessionStart, props.ToArray());
        }

        private void ReportEnded(EndedSession ended)
        {
            if (ended == null) return;
            Enqueue(TelemetryEvents.SessionEnd, ended.Id,
                ("duration_s", ended.DurationSeconds), ("runs", ended.Runs), ("ads", ended.Ads), ("ended_by", ended.EndedBy));
        }

        private void ReportPerf()
        {
            if (_frames.Count >= MinPerfFrames)
            {
                Track(TelemetryEvents.PerfSample, ("fps_median", _frames.MedianFps), ("fps_p5", _frames.P5Fps),
                    ("seconds", Mathf.Round(_frameSeconds)));
            }
            _frames.Clear();
            _frameSeconds = 0f;
        }

        private void Enqueue(string name, string sessionId, params (string Key, object Value)[] props)
        {
            var clean = new Dictionary<string, object>();
            foreach ((string key, object value) in props) clean[key] = Clean(value);
            string id = Guid.NewGuid().ToString();
            string json = JsonConvert.SerializeObject(new EventBody
            {
                Id = id,
                Name = name,
                SessionId = sessionId,
                At = DateTime.UtcNow,
                Props = clean,
            }, ApiClient.Json);
            _queue.Enqueue(id, json);
        }

        /// <summary>Strings cut to the server's limit; non-finite numbers sent as null (else the event is rejected).</summary>
        private static object Clean(object value)
        {
            switch (value)
            {
                case string s: return TelemetryText.Clamp(s);
                case float f: return float.IsNaN(f) || float.IsInfinity(f) ? null : (object)f;
                case double d: return double.IsNaN(d) || double.IsInfinity(d) ? null : (object)d;
                default: return value;
            }
        }

        private async void Flush()
        {
            if (_flushing || _queue.Count == 0 || !_canSend()) return;
            _flushing = true;
            List<QueuedEvent> batch = _queue.Peek();
            var ids = new List<string>(batch.Count);
            var json = new List<string>(batch.Count);
            foreach (QueuedEvent e in batch)
            {
                ids.Add(e.Id);
                json.Add(e.Json);
            }
            try
            {
                TelemetryBatchResponse result = await _api.SendEventsAsync(Application.version, _platform, json);
                _queue.Remove(ids);
                if (result.Rejected > 0) Debug.LogWarning($"Telemetry: the server rejected {result.Rejected} event(s).");
            }
            catch (ApiException e) when (e.IsOffline || e.Status >= 500 || e.Status == 429 || e.Status == 401)
            {
                // Try again on the next flush.
            }
            catch (ApiException e)
            {
                // Any other client error will never pass: drop the batch so it can't block the queue.
                Debug.LogWarning("Telemetry batch dropped: " + e.Code);
                _queue.Remove(ids);
            }
            finally
            {
                _flushing = false;
            }
        }

        private sealed class EventBody
        {
            public string Id;
            public string Name;
            public string SessionId;
            public DateTime At;
            public Dictionary<string, object> Props;
        }
    }
}
