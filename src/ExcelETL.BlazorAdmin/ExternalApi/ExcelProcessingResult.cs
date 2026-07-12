namespace ExcelETL.BlazorAdmin.ExternalApi;

// The caller owns the returned instance and must dispose it: disposing releases both the response
// stream and the underlying HttpResponseMessage, which must stay alive for the stream to remain
// readable (see ExcelProcessingClient.ProcessAsync).
public sealed class ExcelProcessingResult(string fileName, Stream content, HttpResponseMessage response)
    : IDisposable
{
    public string FileName { get; } = fileName;

    public Stream Content { get; } = content;

    public void Dispose()
    {
        Content.Dispose();
        response.Dispose();
    }
}
