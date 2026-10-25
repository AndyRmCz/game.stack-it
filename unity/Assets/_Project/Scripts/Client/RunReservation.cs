using System;
using System.Threading.Tasks;
using StackIt.Client.Api;

namespace StackIt.Client
{
    /// <summary>
    /// The server run for the next normal game, requested one game ahead so pressing Play never
    /// waits for the network. If it isn't back within a short budget (offline, or a server that
    /// never answers), Play goes ahead with an unranked local run, and the request still on its
    /// way serves the next Play if it arrives. A failed request is made again on the next Play.
    /// </summary>
    public sealed class RunReservation
    {
        public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(2);

        private readonly Func<Task<StartRunResponse>> _start;
        private readonly TimeSpan _budget;
        private readonly Func<TimeSpan, Task> _delay;
        private Task<StartRunResponse> _pending;

        public RunReservation(Func<Task<StartRunResponse>> start, TimeSpan? budget = null, Func<TimeSpan, Task> delay = null)
        {
            _start = start;
            _budget = budget ?? DefaultBudget;
            _delay = delay ?? Task.Delay;
        }

        public void Reserve() => _pending = Start();

        /// <summary>The reserved run, or null if none came back in time. Errors within the budget are rethrown.</summary>
        public async Task<StartRunResponse> TakeAsync()
        {
            Task<StartRunResponse> pending = _pending != null && !_pending.IsFaulted && !_pending.IsCanceled ? _pending : Start();
            _pending = null;
            if (!pending.IsCompleted)
            {
                await Task.WhenAny(pending, _delay(_budget));
                if (!pending.IsCompleted)
                {
                    _pending = pending;
                    return null;
                }
            }
            return await pending;
        }

        private Task<StartRunResponse> Start()
        {
            Task<StartRunResponse> task = _start();
            task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted); // never "unobserved"
            return task;
        }
    }
}
