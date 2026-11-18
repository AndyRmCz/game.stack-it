using System;
using System.Threading.Tasks;

namespace StackIt.Client
{
    /// <summary>
    /// Waits for a step that answers through a callback, but not forever: a callback that never
    /// comes (the ads SDK's, at startup) must not stop everything after it.
    /// </summary>
    public static class Deadline
    {
        /// <summary>True if <paramref name="step"/> finished (in any state) within <paramref name="limit"/>.</summary>
        public static async Task<bool> Within(Task step, TimeSpan limit)
        {
            Task first = await Task.WhenAny(step, Task.Delay(limit));
            return first == step;
        }
    }
}
