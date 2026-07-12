using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ExcelETL.BlazorAdmin.Tests.ExternalApi;

// Stands in for the network so tests can assert on the outgoing request and control the response
// without a real Web API process running. Mirrors
// legacy/ExcelProcessingClientService.Tests/FakeHttpMessageHandler.cs.
internal sealed class FakeHttpMessageHandler(
    System.Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
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
