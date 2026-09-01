using System.Net;
using System.Text;
using ExcelETL.BlazorAdmin.Configuration;
using ExcelETL.BlazorAdmin.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Services;

// Lot 038 (38.2): one case per HTTP status OxoController can actually return (confirmed at 38.0
// against GlobalExceptionHandler/OxoController), mapping onto the matching OxoApiTestResult variant.
// No real HTTP call -- FakeHttpMessageHandler stands in for the network, same convention as
// legacy/ExcelProcessingClientService.Tests.
public class OxoApiTestClientTests
{
    private const string ApiKey = "test-api-key-value";

    private static (OxoApiTestClient Client, FakeHttpMessageHandler Handler) CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var fakeHandler = new FakeHttpMessageHandler(handler);
        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://localhost/") };
        var options = Options.Create(new OxoApiTestClientOptions
        {
            BaseUrl = "https://localhost/",
            ApiKey = ApiKey
        });

        return (new OxoApiTestClient(httpClient, options), fakeHandler);
    }

    [Fact]
    public async Task ProcessAsync_WithOkResponse_ReturnsSuccessWithGeneratedFileNameAndContent()
    {
        var expectedBytes = "generated-workbook-bytes"u8.ToArray();
        var (client, _) = CreateClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expectedBytes)
            };
            response.Content.Headers.ContentDisposition =
                new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = "MAD_38-C7401_20260101120000.xlsx"
                };
            return Task.FromResult(response);
        });

        var result = await client.ProcessAsync(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream([1, 2, 3]), "source.xlsx", CancellationToken.None);

        result.Status.Should().Be(OxoApiTestResultStatus.Success);
        result.GeneratedFileName.Should().Be("MAD_38-C7401_20260101120000.xlsx");
        using var reader = new StreamReader(result.GeneratedFileContent!);
        var content = await reader.ReadToEndAsync();
        content.Should().Be("generated-workbook-bytes");
    }

    [Fact]
    public async Task ProcessAsync_WithUnauthorizedResponse_ReturnsUnauthorized()
    {
        var (client, _) = CreateClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var result = await client.ProcessAsync(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), "source.xlsx", CancellationToken.None);

        result.Status.Should().Be(OxoApiTestResultStatus.Unauthorized);
    }

    [Fact]
    public async Task ProcessAsync_WithNotFoundResponse_ReturnsProfileNotFoundWithDetailFromBody()
    {
        var (client, _) = CreateClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(
                    """{"status":404,"detail":"Import profile 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' was not found."}""",
                    Encoding.UTF8, "application/problem+json")
            };
            return Task.FromResult(response);
        });

        var result = await client.ProcessAsync(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), "source.xlsx", CancellationToken.None);

        result.Status.Should().Be(OxoApiTestResultStatus.ProfileNotFound);
        result.ProfileNotFoundDetail.Should().Contain("Import profile");
    }

    [Fact]
    public async Task ProcessAsync_WithUnprocessableEntityResponse_ReturnsBusinessRejectionWithErrors()
    {
        var (client, _) = CreateClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent(
                    """
                    {"status":422,"detail":"The file was rejected.",
                     "errors":[{"sheet":"PROCEDURE","blockIdentifier":"M2:O2","code":"RequiredFieldMissing","message":"Repere is required."}]}
                    """,
                    Encoding.UTF8, "application/problem+json")
            };
            return Task.FromResult(response);
        });

        var result = await client.ProcessAsync(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), "source.xlsx", CancellationToken.None);

        result.Status.Should().Be(OxoApiTestResultStatus.BusinessRejection);
        result.RejectionErrors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new OxoApiTestRejectionError("PROCEDURE", "M2:O2", "RequiredFieldMissing", "Repere is required."));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ProcessAsync_WithAnyOtherErrorStatus_ReturnsTechnicalErrorWithStatusCode(HttpStatusCode statusCode)
    {
        var (client, _) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(statusCode)));

        var result = await client.ProcessAsync(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), "source.xlsx", CancellationToken.None);

        result.Status.Should().Be(OxoApiTestResultStatus.TechnicalError);
        result.HttpStatusCode.Should().Be((int)statusCode);
    }

    // Lot 065.2: GlobalExceptionHandler (Lot 065.1) surfaces the unmapped exception's short type
    // name and message as ProblemDetails extensions on a 500 -- read here into the result so
    // ApiTest.razor can display them directly.
    [Fact]
    public async Task ProcessAsync_WithInternalServerErrorCarryingExceptionDetail_ReturnsTechnicalErrorWithTypeAndMessage()
    {
        var (client, _) = CreateClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(
                    """
                    {"status":500,"exceptionType":"UnknownFieldReferenceException",
                     "exceptionMessage":"Field 'HasZeroEnergie' was not found among the already-extracted fields."}
                    """,
                    Encoding.UTF8, "application/problem+json")
            };
            return Task.FromResult(response);
        });

        var result = await client.ProcessAsync(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), "source.xlsx", CancellationToken.None);

        result.Status.Should().Be(OxoApiTestResultStatus.TechnicalError);
        result.HttpStatusCode.Should().Be(500);
        result.ExceptionType.Should().Be("UnknownFieldReferenceException");
        result.ExceptionMessage.Should().Be("Field 'HasZeroEnergie' was not found among the already-extracted fields.");
    }

    // Explicit repli case (65.2): a 500 with an empty/non-parsable body must not throw and must
    // leave ExceptionType/ExceptionMessage null, letting the page fall back to its generic message.
    [Fact]
    public async Task ProcessAsync_WithInternalServerErrorAndEmptyBody_ReturnsTechnicalErrorWithNoExceptionDetail()
    {
        var (client, _) = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var result = await client.ProcessAsync(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), "source.xlsx", CancellationToken.None);

        result.Status.Should().Be(OxoApiTestResultStatus.TechnicalError);
        result.ExceptionType.Should().BeNull();
        result.ExceptionMessage.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_WhenNoConnectionCanBeEstablished_ReturnsConnectionErrorInsteadOfThrowing()
    {
        var (client, _) = CreateClient(_ => throw new HttpRequestException(
            "No connection could be made because the target machine actively refused it."));

        var result = await client.ProcessAsync(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), "source.xlsx", CancellationToken.None);

        result.Status.Should().Be(OxoApiTestResultStatus.ConnectionError);
    }

    [Fact]
    public async Task ProcessAsync_WhenRequestTimesOut_ReturnsConnectionErrorInsteadOfThrowing()
    {
        var (client, _) = CreateClient(_ => throw new TaskCanceledException("The request timed out."));

        var result = await client.ProcessAsync(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), "source.xlsx", CancellationToken.None);

        result.Status.Should().Be(OxoApiTestResultStatus.ConnectionError);
    }

    [Fact]
    public async Task ProcessAsync_WhenCallerCancels_StillThrowsTaskCanceledException()
    {
        var (client, _) = CreateClient(_ => throw new TaskCanceledException("Cancelled."));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => client.ProcessAsync(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), "source.xlsx", cts.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task ProcessAsync_SendsApiKeyHeaderWithConfiguredValue()
    {
        var (client, handler) = CreateClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        await client.ProcessAsync(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), "source.xlsx", CancellationToken.None);

        handler.LastRequest!.Headers.TryGetValues("X-Api-Key", out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be(ApiKey);
    }

    [Fact]
    public async Task ProcessAsync_BuildsMultipartRequestWithExpectedFieldNames()
    {
        var (client, handler) = CreateClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var importProfileId = Guid.NewGuid();
        var exportProfileId = Guid.NewGuid();

        await client.ProcessAsync(importProfileId, exportProfileId, new MemoryStream([1, 2, 3]), "source.xlsx", CancellationToken.None);

        handler.LastRequestBody.Should().Contain("ImportProfileId").And.Contain(importProfileId.ToString());
        handler.LastRequestBody.Should().Contain("ExportProfileId").And.Contain(exportProfileId.ToString());
        handler.LastRequestBody.Should().Contain("name=File").And.Contain("source.xlsx");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/api/oxo/process");
    }
}
