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
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ImportProfileIdFieldName = "ImportProfileId";
    private const string ExportProfileIdFieldName = "ExportProfileId";
    private const string FileFieldName = "File";

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

            default:
                return OxoApiTestResult.TechnicalError((int)response.StatusCode);
        }
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
    }
}
