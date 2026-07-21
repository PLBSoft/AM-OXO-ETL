using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;

namespace Legacy.ExcelProcessingClientService
{
    /// <summary>
    /// Represents the legacy ASP.NET MVC 5 / .NET Framework 4.8 application's client for
    /// synchronously submitting an uploaded Excel file to the new ExcelETL Web API's OXO
    /// pipeline and streaming back the generated workbook, authenticating via the X-Api-Key
    /// header. Migrated at Lot K3 from the retired POC route (api/excel/process,
    /// ExtractionConfigId) to api/oxo/process (ImportProfileId/ExportProfileId) -- a direct,
    /// one-shot contract change, not a parallel/feature-flagged rollout.
    /// </summary>
    public class ExcelProcessingClientService : IDisposable
    {
        private const string ApiKeyHeaderName = "X-Api-Key";
        private const string ProcessRelativeUrl = "api/oxo/process";
        private const string ImportProfileIdFieldName = "ImportProfileId";
        private const string ExportProfileIdFieldName = "ExportProfileId";
        private const string FileFieldName = "File";

        // Extraction is synchronous and can take several seconds to a few minutes for large,
        // heavily merged-cell workbooks. The Web API's own request-timeout policy allows up to
        // 5 minutes (see ExcelETL.WebAPI.UploadLimits.ExcelProcessingTimeout); this client's
        // timeout is set comfortably above that so the legacy caller never gives up before the
        // server does, while still exceeding the 120-second floor required by this milestone.
        internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(6);

        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;

        public ExcelProcessingClientService(string baseUrl, string apiKey)
            : this(CreateHttpClient(baseUrl, apiKey), ownsHttpClient: true)
        {
        }

        internal ExcelProcessingClientService(HttpClient httpClient, bool ownsHttpClient)
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

        /// <summary>
        /// Uploads <paramref name="file"/> for extraction and generation under
        /// <paramref name="importProfileId"/>/<paramref name="exportProfileId"/> and returns the
        /// generated workbook streamed back in the response body. The caller owns the returned
        /// <see cref="ExcelProcessingResult"/> and must dispose it.
        /// </summary>
        public async Task<ExcelProcessingResult> ProcessAsync(
            Guid importProfileId, Guid exportProfileId, HttpPostedFileBase file)
        {
            if (file == null)
            {
                throw new ArgumentNullException("file");
            }

            if (file.ContentLength == 0)
            {
                throw new ArgumentException("Uploaded file must not be empty.", "file");
            }

            using (var content = BuildRequestContent(importProfileId, exportProfileId, file))
            {
                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.PostAsync(ProcessRelativeUrl, content).ConfigureAwait(false);
                }
                catch (TaskCanceledException ex)
                {
                    // HttpClient throws a bare TaskCanceledException both for an elapsed Timeout
                    // and for explicit cancellation; since ProcessAsync accepts no CancellationToken
                    // of its own, any TaskCanceledException here can only originate from the
                    // configured Timeout, so it is safe to always translate it.
                    throw new TimeoutException(
                        string.Format(
                            "The Excel processing request timed out after {0}. The Web API may still be " +
                            "processing the file; check the extraction history before retrying.",
                            _httpClient.Timeout),
                        ex);
                }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new HttpRequestException(string.Format(
                            "Excel processing request failed with status {0} ({1}): {2}",
                            (int)response.StatusCode, response.ReasonPhrase, errorBody));
                    }

                    var resultFileName = ExtractFileName(response) ?? file.FileName;
                    var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    return new ExcelProcessingResult(resultFileName, new MemoryStream(bytes));
                }
            }
        }

        private static MultipartFormDataContent BuildRequestContent(
            Guid importProfileId, Guid exportProfileId, HttpPostedFileBase file)
        {
            var content = new MultipartFormDataContent();
            content.Add(new StringContent(importProfileId.ToString()), ImportProfileIdFieldName);
            content.Add(new StringContent(exportProfileId.ToString()), ExportProfileIdFieldName);

            var fileContent = new StreamContent(file.InputStream);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
            content.Add(fileContent, FileFieldName, file.FileName);

            return content;
        }

        private static string ExtractFileName(HttpResponseMessage response)
        {
            var disposition = response.Content.Headers.ContentDisposition;
            return disposition != null ? disposition.FileName : null;
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
