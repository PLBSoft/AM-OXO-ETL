namespace ExcelETL.BlazorAdmin.ExternalApi;

// Isolates the JS-interop/stream-marshaling boundary from UploadTest.razor so tests can mock it
// directly instead of exercising DotNetStreamReference through a real browser circuit, which
// bUnit's fake IJSRuntime cannot support.
public interface IExcelDownloadInterop
{
    Task DownloadFileFromStreamAsync(string fileName, Stream content);
}
