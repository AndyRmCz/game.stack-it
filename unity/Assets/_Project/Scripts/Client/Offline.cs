namespace StackIt.Client
{
    /// <summary>
    /// Whether an error means the phone couldn't reach the server. A request that got no answer has
    /// no HTTP status and this code (ApiClient); other errors without a status, like Google's own
    /// sign-in failing, happen on a phone that may well be online, and say what really failed.
    /// </summary>
    public static class Offline
    {
        public const string Code = "offline";

        public static bool Is(int status, string code) => status == 0 && code == Code;
    }
}
