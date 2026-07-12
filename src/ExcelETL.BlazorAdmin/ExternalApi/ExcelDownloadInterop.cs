using Microsoft.JSInterop;

namespace ExcelETL.BlazorAdmin.ExternalApi;

public sealed class ExcelDownloadInterop(IJSRuntime jsRuntime) : IExcelDownloadInterop
{
    public async Task DownloadFileFromStreamAsync(string fileName, Stream content)
    {
        await using var module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/fileDownload.js");
        var streamRef = new DotNetStreamReference(content, leaveOpen: true);
        await module.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
    }
}
