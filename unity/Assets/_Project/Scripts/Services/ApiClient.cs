using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using StackIt.Client;
using StackIt.Client.Api;
using UnityEngine.Networking;

namespace StackIt.Services
{
    /// <summary>Supplies and renews the bearer token. Implemented by AuthService.</summary>
    public interface ITokenProvider
    {
        string AccessToken { get; }

        /// <summary>Gets a fresh access token after a 401. False if the session is gone.</summary>
        Task<bool> RenewAsync();
    }

    /// <summary>
    /// Typed JSON client for the backend: snake_case JSON, bearer auth with one transparent
    /// token renewal on 401, retries with exponential backoff for network errors and 5xx/429.
    /// </summary>
    public sealed class ApiClient
    {
        public static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        private const int TimeoutSeconds = 12;

        private readonly string _baseUrl;
        private readonly RetryPolicy _retry;

        public ITokenProvider Tokens { get; set; }

        public ApiClient(string baseUrl, RetryPolicy retry = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _retry = retry ?? new RetryPolicy();
        }

        public string BaseUrl => _baseUrl;

        public Task<T> Get<T>(string path, bool auth = true) => Send<T>("GET", path, null, auth, null);

        public Task<T> Post<T>(string path, object body, bool auth = true, string idempotencyKey = null) =>
            Send<T>("POST", path, body, auth, idempotencyKey);

        public Task<T> Patch<T>(string path, object body) => Send<T>("PATCH", path, body, true, null);

        public Task Delete(string path) => Send<object>("DELETE", path, null, true, null);

        private async Task<T> Send<T>(string method, string path, object body, bool auth, string idempotencyKey)
        {
            string json = body == null ? null : JsonConvert.SerializeObject(body, Json);
            bool renewed = false;
            for (int attempt = 1; ; attempt++)
            {
                using (UnityWebRequest request = Build(method, path, json, auth, idempotencyKey))
                {
                    await request.SendWebRequest().AsTask();
                    long status = request.responseCode;
                    bool networkError = request.result == UnityWebRequest.Result.ConnectionError;

                    if (!networkError && status >= 200 && status < 300)
                    {
                        string text = request.downloadHandler?.text;
                        if (typeof(T) == typeof(object) || string.IsNullOrEmpty(text)) return default;
                        return JsonConvert.DeserializeObject<T>(text, Json);
                    }

                    if (status == 401 && auth && !renewed && Tokens != null)
                    {
                        renewed = true;
                        if (await Tokens.RenewAsync()) continue;
                    }

                    bool transient = networkError || RetryPolicy.IsTransientStatus((int)status);
                    if (transient && _retry.CanRetry(attempt))
                    {
                        await Delay(_retry.DelaySeconds(attempt));
                        continue;
                    }
                    throw ToException(request, networkError);
                }
            }
        }

        private UnityWebRequest Build(string method, string path, string json, bool auth, string idempotencyKey)
        {
            var request = new UnityWebRequest(_baseUrl + path, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            };
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SetRequestHeader("Accept", "application/json");
            if (auth && Tokens?.AccessToken != null)
                request.SetRequestHeader("Authorization", "Bearer " + Tokens.AccessToken);
            if (idempotencyKey != null) request.SetRequestHeader("Idempotency-Key", idempotencyKey);
            return request;
        }

        private static ApiException ToException(UnityWebRequest request, bool networkError)
        {
            if (networkError) return new ApiException(0, "offline", request.error);
            try
            {
                var envelope = JsonConvert.DeserializeObject<ErrorEnvelope>(request.downloadHandler.text, Json);
                if (envelope?.Error != null)
                    return new ApiException((int)request.responseCode, envelope.Error.Code, envelope.Error.Message);
            }
            catch (JsonException)
            {
                // Not our JSON error body (proxy page, validation error): fall through.
            }
            return new ApiException((int)request.responseCode, "http_" + request.responseCode, request.error);
        }

        // Unity's synchronization context resumes the await on the main thread.
        private static Task Delay(double seconds) => Task.Delay(TimeSpan.FromSeconds(seconds));
    }

    public static class UnityWebRequestExtensions
    {
        public static Task AsTask(this UnityWebRequestAsyncOperation operation)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (operation.isDone) tcs.SetResult(true);
            else operation.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }
    }
}
