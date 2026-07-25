namespace ExcelETL.BlazorAdmin.Tests.Services;

// Lot 038 (38.0): mirrors legacy/ExcelProcessingClientService.Tests/FakeHttpMessageHandler.cs
// verbatim (same "mock the endpoint, no new HTTP-mocking dependency" convention) rather than
// introducing RichardSzalay.MockHttp for equivalent capability -- colocated per test project like
// that precedent, not shared across the two.
internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return await handler(request);
    }
}
