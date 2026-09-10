using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ExcelETL.BlazorAdmin.Configuration;
using Microsoft.Extensions.Options;

namespace ExcelETL.BlazorAdmin.Services;

// Lot 038 (38.2): calls the real POST /api/oxo/process contract confirmed in 38.0 against
// OxoController/ApiKeyAuthenticationHandler -- same header name/multipart field names as the legacy
// ExcelProcessingClientService (X-Api-Key, ImportProfileId/ExportProfileId/File), reproduced here
// rather than referenced, since BlazorAdmin never references ExcelETL.WebAPI/legacy directly. No
// business logic here (no validation duplicated from OxoController) -- this type only calls the API
// and maps the HTTP response onto an OxoApiTestResult variant.
public sealed class OxoApiTestClient(HttpClient httpClient, IOptions<OxoApiTestClientOptions> options)
    : IOxoApiTestClient
{
    private const string ProcessRelativeUrl = "api/oxo/process";
    private const string HealthRelativeUrl = "api/health";
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ImportProfileIdFieldName = "ImportProfileId";
    private const string ExportProfileIdFieldName = "ExportProfileId";
    private const string FileFieldName = "File";

    // GET api/health is a lightweight diagnostic, not the several-minute extraction call
    // ProcessAsync makes -- bounded well below the HttpClient's own 6-minute Timeout (Program.cs)
    // so a down/unreachable API never makes the page hang waiting for this one status line.
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<OxoApiTestResult> ProcessAsync(
        Guid importProfileId, Guid exportProfileId, Stream fileContent, string fileName,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(importProfileId.ToString()), ImportProfileIdFieldName },
            { new StringContent(exportProfileId.ToString()), ExportProfileIdFieldName }
        };

        var fileStreamContent = new StreamContent(fileContent);
        fileStreamContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileStreamContent, FileFieldName, fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, ProcessRelativeUrl) { Content = content };
        request.Headers.Add(ApiKeyHeaderName, options.Value.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // No HTTP response was ever received (server not running, wrong BaseUrl, firewall) --
            // never let this propagate: it would crash the whole Blazor Server circuit instead of
            // surfacing an inline message on the page. See OxoApiTestResult's own comment.
            return OxoApiTestResult.ConnectionError();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient throws TaskCanceledException (not TimeoutException) when its own Timeout
            // elapses without the caller having cancelled -- same "no response, don't crash the
            // circuit" treatment as a refused connection.
            return OxoApiTestResult.ConnectionError();
        }

        using var _ = response;
        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                var generatedFileName = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName
                    ?? fileName;
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                return OxoApiTestResult.Success(new MemoryStream(bytes), Unquote(generatedFileName));

            case HttpStatusCode.Unauthorized:
                return OxoApiTestResult.Unauthorized();

            case HttpStatusCode.NotFound:
                var notFoundDetail = await TryReadProblemDetailsAsync(response, cancellationToken);
                return OxoApiTestResult.ProfileNotFound(notFoundDetail?.Detail);

            case HttpStatusCode.UnprocessableEntity:
                var rejectionBody = await TryReadProblemDetailsAsync(response, cancellationToken);
                return OxoApiTestResult.BusinessRejection(rejectionBody?.Errors ?? []);

            case HttpStatusCode.InternalServerError:
                // Lot 065: GlobalExceptionHandler now surfaces the exception's short type name and
                // message for any unmapped exception -- both stay null (falling back to the page's
                // pre-existing generic message) when the body isn't a parsable ProblemDetails
                // carrying them, e.g. an empty response.
                var technicalErrorBody = await TryReadProblemDetailsAsync(response, cancellationToken);
                return OxoApiTestResult.TechnicalError(
                    (int)response.StatusCode, technicalErrorBody?.ExceptionType, technicalErrorBody?.ExceptionMessage);

            default:
                return OxoApiTestResult.TechnicalError((int)response.StatusCode);
        }
    }

    public async Task<OxoApiHealthResult> GetHealthAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(HealthCheckTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, HealthRelativeUrl);
        request.Headers.Add(ApiKeyHeaderName, options.Value.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
        }
        catch (HttpRequestException)
        {
            return OxoApiHealthResult.Unreachable();
        }
        catch (TaskCanceledException)
        {
            // Unlike ProcessAsync, this method's whole contract is "always answer quickly, never
            // hang" -- a caller-cancel and the internal 5s timeout are treated identically here
            // (both surfaced as Unreachable), rather than distinguished/rethrown.
            return OxoApiHealthResult.Unreachable();
        }

        using var _ = response;
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return OxoApiHealthResult.Unreachable();
        }

        HealthResponseBody? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<HealthResponseBody>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return OxoApiHealthResult.Unreachable();
        }

        return body is null
            ? OxoApiHealthResult.Unreachable()
            : OxoApiHealthResult.Reachable(body.Status ?? "", body.Version ?? "", body.Database ?? "");
    }

    private static string Unquote(string fileName) => fileName.Trim('"');

    private static async Task<ProblemDetailsBody?> TryReadProblemDetailsAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemDetailsBody>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class ProblemDetailsBody
    {
        public string? Detail { get; set; }

        public List<OxoApiTestRejectionError>? Errors { get; set; }

        public string? ExceptionType { get; set; }

        public string? ExceptionMessage { get; set; }
    }

    private sealed class HealthResponseBody
    {
        public string? Status { get; set; }

        public string? Version { get; set; }

        public string? Database { get; set; }
    }
}
