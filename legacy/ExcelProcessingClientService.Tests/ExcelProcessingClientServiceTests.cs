using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Legacy.ExcelProcessingClientService.Tests
{
    public class ExcelProcessingClientServiceTests
    {
        private const string BaseUrl = "https://excel-etl.internal/";
        private const string ApiKey = "legacy-app-api-key";
        private static readonly Guid ExtractionConfigId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        [Fact]
        public void Constructor_ConfiguresBaseAddress()
        {
            using (var service = new ExcelProcessingClientService(BaseUrl, ApiKey))
            {
                service.Client.BaseAddress.Should().Be(new Uri(BaseUrl));
            }
        }

        [Fact]
        public void Constructor_ConfiguresTimeoutOfAtLeastTwoMinutes()
        {
            using (var service = new ExcelProcessingClientService(BaseUrl, ApiKey))
            {
                service.Client.Timeout.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(120));
            }
        }

        [Fact]
        public void Constructor_SetsApiKeyHeader()
        {
            using (var service = new ExcelProcessingClientService(BaseUrl, ApiKey))
            {
                service.Client.DefaultRequestHeaders.GetValues("X-Api-Key").Should().ContainSingle(ApiKey);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_WithInvalidBaseUrl_ThrowsArgumentException(string invalidBaseUrl)
        {
            Action act = () => new ExcelProcessingClientService(invalidBaseUrl, ApiKey);

            act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("baseUrl");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_WithInvalidApiKey_ThrowsArgumentException(string invalidApiKey)
        {
            Action act = () => new ExcelProcessingClientService(BaseUrl, invalidApiKey);

            act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("apiKey");
        }

        [Fact]
        public async Task ProcessAsync_WithNullFile_ThrowsArgumentNullException()
        {
            using (var service = new ExcelProcessingClientService(BaseUrl, ApiKey))
            {
                Func<Task> act = async () => await service.ProcessAsync(ExtractionConfigId, null);

                (await act.Should().ThrowAsync<ArgumentNullException>()).Which.ParamName.Should().Be("file");
            }
        }

        [Fact]
        public async Task ProcessAsync_WithEmptyFile_ThrowsArgumentException()
        {
            using (var service = new ExcelProcessingClientService(BaseUrl, ApiKey))
            {
                var emptyFile = new FakeHttpPostedFile("empty.xlsx", "application/octet-stream", new MemoryStream());

                Func<Task> act = async () => await service.ProcessAsync(ExtractionConfigId, emptyFile);

                (await act.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be("file");
            }
        }

        [Fact]
        public async Task ProcessAsync_PostsMultipartRequestWithConfigIdAndFileToProcessEndpoint()
        {
            var fakeHandler = new FakeHttpMessageHandler(_ => Task.FromResult(SuccessResponse(new byte[] { 1, 2, 3 })));
            using (var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromMinutes(5) })
            using (var service = new ExcelProcessingClientService(httpClient, ownsHttpClient: false))
            using (var sourceStream = new MemoryStream(new byte[] { 9, 9, 9 }))
            {
                var file = new FakeHttpPostedFile("invoice.xlsx", "application/vnd.ms-excel", sourceStream);

                using (await service.ProcessAsync(ExtractionConfigId, file))
                {
                    fakeHandler.LastRequest.Method.Should().Be(HttpMethod.Post);
                    fakeHandler.LastRequest.RequestUri.Should().Be(new Uri(new Uri(BaseUrl), "api/excel/process"));
                    fakeHandler.LastRequestBody.Should().Contain("name=ExtractionConfigId");
                    fakeHandler.LastRequestBody.Should().Contain(ExtractionConfigId.ToString());
                    fakeHandler.LastRequestBody.Should().Contain("name=File; filename=invoice.xlsx");
                }
            }
        }

        [Fact]
        public async Task ProcessAsync_OnSuccess_ReturnsFileNameAndStreamedContent()
        {
            var responseBytes = Encoding.UTF8.GetBytes("fake-xlsx-bytes");
            var fakeHandler = new FakeHttpMessageHandler(_ => Task.FromResult(
                SuccessResponse(responseBytes, "processed-invoice.xlsx")));
            using (var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromMinutes(5) })
            using (var service = new ExcelProcessingClientService(httpClient, ownsHttpClient: false))
            using (var sourceStream = new MemoryStream(new byte[] { 9, 9, 9 }))
            {
                var file = new FakeHttpPostedFile("invoice.xlsx", "application/vnd.ms-excel", sourceStream);

                using (var result = await service.ProcessAsync(ExtractionConfigId, file))
                {
                    result.FileName.Should().Be("processed-invoice.xlsx");

                    using (var reader = new MemoryStream())
                    {
                        await result.Content.CopyToAsync(reader);
                        reader.ToArray().Should().Equal(responseBytes);
                    }
                }
            }
        }

        [Fact]
        public async Task ProcessAsync_OnNotFoundResponse_ThrowsHttpRequestExceptionWithStatusAndBody()
        {
            var fakeHandler = new FakeHttpMessageHandler(_ => Task.FromResult(
                ErrorResponse(HttpStatusCode.NotFound, "{\"message\":\"Extraction config not found.\"}")));
            using (var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromMinutes(5) })
            using (var service = new ExcelProcessingClientService(httpClient, ownsHttpClient: false))
            using (var sourceStream = new MemoryStream(new byte[] { 9, 9, 9 }))
            {
                var file = new FakeHttpPostedFile("invoice.xlsx", "application/vnd.ms-excel", sourceStream);

                Func<Task> act = async () => await service.ProcessAsync(ExtractionConfigId, file);

                var exception = await act.Should().ThrowAsync<HttpRequestException>();
                exception.Which.Message.Should().Contain("404");
                exception.Which.Message.Should().Contain("Extraction config not found");
            }
        }

        [Fact]
        public async Task ProcessAsync_WhenRequestTimesOut_ThrowsTimeoutException()
        {
            var fakeHandler = new FakeHttpMessageHandler(_ =>
            {
                throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
            });
            using (var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromMinutes(5) })
            using (var service = new ExcelProcessingClientService(httpClient, ownsHttpClient: false))
            using (var sourceStream = new MemoryStream(new byte[] { 9, 9, 9 }))
            {
                var file = new FakeHttpPostedFile("invoice.xlsx", "application/vnd.ms-excel", sourceStream);

                Func<Task> act = async () => await service.ProcessAsync(ExtractionConfigId, file);

                await act.Should().ThrowAsync<TimeoutException>();
            }
        }

        private static HttpResponseMessage SuccessResponse(byte[] bytes, string fileName = "processed.xlsx")
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentDisposition =
                new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = fileName
                };
            return response;
        }

        private static HttpResponseMessage ErrorResponse(HttpStatusCode statusCode, string body)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
