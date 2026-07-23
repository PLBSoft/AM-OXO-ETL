namespace ExcelETL.BlazorAdmin.Excel;

// IBrowserFile.OpenReadStream() returns a stream that only supports asynchronous reads -- the
// upload is streamed live from the browser over the Interactive Server circuit, one chunk at a
// time -- unlike a real FileStream or WebAPI's IFormFile.OpenReadStream() (already fully buffered
// to memory/disk by multipart model binding before the controller runs). ClosedXML's
// XLWorkbook(Stream) constructor opens the .xlsx as a zip package via synchronous Stream.Read()
// calls, which throws "Synchronous reads are not supported." the instant it touches a browser
// file stream. Buffering the whole upload into a seekable MemoryStream first (via CopyToAsync,
// the only thing the browser stream actually supports) sidesteps this entirely -- and is also why
// this never surfaced in bUnit: its InputFileContent test double is backed by a plain byte array,
// which supports synchronous reads just fine.
public static class BrowserFileStreamBuffering
{
    public static async Task<MemoryStream> BufferToSeekableStreamAsync(
        Stream source, CancellationToken cancellationToken = default)
    {
        var buffered = new MemoryStream();
        await source.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;
        return buffered;
    }
}
