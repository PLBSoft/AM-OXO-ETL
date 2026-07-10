using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Legacy.NewApiPingService
{
    /// <summary>
    /// Represents the legacy ASP.NET MVC 5 / .NET Framework 4.8 application's client for
    /// poking the new ExcelETL Web API over HTTP, authenticating via the X-Api-Key header.
    /// </summary>
    public class NewApiPingService : IDisposable
    {
        private const string ApiKeyHeaderName = "X-Api-Key";
        private const string PingRelativeUrl = "api/health/ping";

        // Excel extraction requests are processed synchronously and can take several seconds;
        // the timeout is set generously to accommodate future synchronous processing calls.
        internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;

        public NewApiPingService(string baseUrl, string apiKey)
            : this(CreateHttpClient(baseUrl, apiKey), ownsHttpClient: true)
        {
        }

        internal NewApiPingService(HttpClient httpClient, bool ownsHttpClient)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            _httpClient = httpClient;
            _ownsHttpClient = ownsHttpClient;
        }

        internal HttpClient Client
        {
            get { return _httpClient; }
        }

        private static HttpClient CreateHttpClient(string baseUrl, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base URL must not be empty.", "baseUrl");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key must not be empty.", "apiKey");
            }

            var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = DefaultTimeout
            };
            client.DefaultRequestHeaders.Add(ApiKeyHeaderName, apiKey);
            return client;
        }

        public async Task<string> PingAsync()
        {
            using (var response = await _httpClient.GetAsync(PingRelativeUrl).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }
    }
}
