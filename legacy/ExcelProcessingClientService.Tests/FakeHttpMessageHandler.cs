using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Legacy.ExcelProcessingClientService.Tests
{
    // Stands in for the network so tests can assert on the outgoing request and control the
    // response without a real Web API process running.
    internal class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        public HttpRequestMessage LastRequest { get; private set; }

        public string LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            return await _handler(request).ConfigureAwait(false);
        }
    }
}
