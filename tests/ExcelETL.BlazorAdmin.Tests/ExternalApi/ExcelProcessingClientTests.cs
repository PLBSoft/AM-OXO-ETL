using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using ExcelETL.BlazorAdmin.ExternalApi;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.ExternalApi;

public class ExcelProcessingClientTests
{
    private const string BaseUrl = "http://localhost/";
    private static readonly Guid ExtractionConfigId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void DefaultTimeout_IsComfortablyAboveTheWebApisFiveMinuteProcessingPolicy()
    {
        ExcelProcessingClient.DefaultTimeout.Should().BeGreaterThan(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task ProcessAsync_SendsApiKeyHeader()
    {
        var fakeHandler = new FakeHttpMessageHandler(_ => Task.FromResult(SuccessResponse([1, 2, 3])));
        using var httpClient = CreateHttpClient(fakeHandler, apiKey: "test-api-key");
        var client = new ExcelProcessingClient(httpClient);
        using var sourceStream = new MemoryStream([9, 9, 9]);

        using (await client.ProcessAsync(ExtractionConfigId, sourceStream, "invoice.xlsx", "application/vnd.ms-excel"))
        {
            fakeHandler.LastRequest!.Headers.GetValues("X-Api-Key").Should().ContainSingle("test-api-key");
        }
    }

    [Fact]
    public async Task ProcessAsync_PostsMultipartRequestWithConfigIdAndFileToProcessEndpoint()
    {
        var fakeHandler = new FakeHttpMessageHandler(_ => Task.FromResult(SuccessResponse([1, 2, 3])));
        using var httpClient = CreateHttpClient(fakeHandler);
        var client = new ExcelProcessingClient(httpClient);
        using var sourceStream = new MemoryStream([9, 9, 9]);

        using (await client.ProcessAsync(ExtractionConfigId, sourceStream, "invoice.xlsx", "application/vnd.ms-excel"))
        {
            fakeHandler.LastRequest!.Method.Should().Be(HttpMethod.Post);
            fakeHandler.LastRequest.RequestUri.Should().Be(new Uri(new Uri(BaseUrl), "api/excel/process"));
            fakeHandler.LastRequestBody.Should().Contain("name=ExtractionConfigId");
            fakeHandler.LastRequestBody.Should().Contain(ExtractionConfigId.ToString());
            fakeHandler.LastRequestBody.Should().Contain("name=File; filename=invoice.xlsx");
        }
    }

    [Fact]
    public async Task ProcessAsync_OnSuccess_ReturnsFileNameAndStreamedContent()
    {
        var responseBytes = Encoding.UTF8.GetBytes("fake-xlsx-bytes");
        var fakeHandler = new FakeHttpMessageHandler(_ => Task.FromResult(
            SuccessResponse(responseBytes, "processed-invoice.xlsx")));
        using var httpClient = CreateHttpClient(fakeHandler);
        var client = new ExcelProcessingClient(httpClient);
        using var sourceStream = new MemoryStream([9, 9, 9]);

        using var result = await client.ProcessAsync(ExtractionConfigId, sourceStream, "invoice.xlsx", "application/vnd.ms-excel");

        result.FileName.Should().Be("processed-invoice.xlsx");

        using var reader = new MemoryStream();
        await result.Content.CopyToAsync(reader);
        reader.ToArray().Should().Equal(responseBytes);
    }

    [Fact]
    public async Task ProcessAsync_OnErrorResponse_ThrowsHttpRequestExceptionWithStatusAndBody()
    {
        var fakeHandler = new FakeHttpMessageHandler(_ => Task.FromResult(
            ErrorResponse(HttpStatusCode.NotFound, "{\"detail\":\"Extraction config not found.\"}")));
        using var httpClient = CreateHttpClient(fakeHandler);
        var client = new ExcelProcessingClient(httpClient);
        using var sourceStream = new MemoryStream([9, 9, 9]);

        Func<Task> act = async () => await client.ProcessAsync(ExtractionConfigId, sourceStream, "invoice.xlsx", "application/vnd.ms-excel");

        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.Message.Should().Contain("404");
        exception.Which.Message.Should().Contain("Extraction config not found");
    }

    [Fact]
    public async Task ProcessAsync_WhenRequestTimesOut_ThrowsTimeoutException()
    {
        var fakeHandler = new FakeHttpMessageHandler(_ =>
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));
        using var httpClient = CreateHttpClient(fakeHandler);
        var client = new ExcelProcessingClient(httpClient);
        using var sourceStream = new MemoryStream([9, 9, 9]);

        Func<Task> act = async () => await client.ProcessAsync(ExtractionConfigId, sourceStream, "invoice.xlsx", "application/vnd.ms-excel");

        await act.Should().ThrowAsync<TimeoutException>();
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler, string apiKey = "test-api-key")
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private static HttpResponseMessage SuccessResponse(byte[] bytes, string fileName = "processed.xlsx")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = fileName
        };
        return response;
    }

    private static HttpResponseMessage ErrorResponse(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
