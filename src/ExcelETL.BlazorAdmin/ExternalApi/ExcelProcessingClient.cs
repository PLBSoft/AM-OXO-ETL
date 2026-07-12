using System.Net.Http.Headers;

namespace ExcelETL.BlazorAdmin.ExternalApi;

// Milestone 10 (see CLAUDE.md "Architectural Oversight" discussion): BlazorAdmin normally must
// never talk to the Web API over HTTP -- both hosts are supposed to share data exclusively
// through Application-layer services/repositories. This client is a deliberate, narrow exception
// to that rule: the /upload-test admin page exists specifically to exercise the real
// API-key-guarded M2M HTTP contract the legacy app uses, not to fetch or mutate domain data. Do
// not use this as precedent for any other BlazorAdmin <-> WebAPI interaction.
//
// Shape and timeout precedent mirrored from legacy/ExcelProcessingClientService (the legacy app's
// own client for this same endpoint): the Web API's own processing timeout policy is 5 minutes
// (ExcelETL.WebAPI.UploadLimits.ExcelProcessingTimeout), so this client's timeout is set
// comfortably above that so it never gives up before the server does.
public class ExcelProcessingClient(HttpClient httpClient)
{
    internal const string ApiKeyHeaderName = "X-Api-Key";
    private const string ProcessRelativeUrl = "api/excel/process";
    private const string ExtractionConfigIdFieldName = "ExtractionConfigId";
    private const string FileFieldName = "File";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(6);

    public async Task<ExcelProcessingResult> ProcessAsync(
        Guid extractionConfigId, Stream fileStream, string fileName, string contentType,
        CancellationToken cancellationToken = default)
    {
        using var content = BuildRequestContent(extractionConfigId, fileStream, fileName, contentType);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(ProcessRelativeUrl, content, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient throws a bare TaskCanceledException both for an elapsed Timeout and for
            // explicit cancellation; the `when` clause above rules out the latter, so this can
            // only be the configured Timeout.
            throw new TimeoutException(
                $"The Excel processing request timed out after {httpClient.Timeout}. The Web API " +
                "may still be processing the file; check the extraction history before retrying.",
                ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            throw new HttpRequestException(
                $"Excel processing request failed with status {(int)response.StatusCode} " +
                $"({response.ReasonPhrase}): {errorBody}");
        }

        var resultFileName = response.Content.Headers.ContentDisposition?.FileName ?? fileName;
        var resultStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return new ExcelProcessingResult(resultFileName, resultStream, response);
    }

    private static MultipartFormDataContent BuildRequestContent(
        Guid extractionConfigId, Stream fileStream, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(extractionConfigId.ToString()), ExtractionConfigIdFieldName }
        };

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        content.Add(fileContent, FileFieldName, fileName);

        return content;
    }
}
