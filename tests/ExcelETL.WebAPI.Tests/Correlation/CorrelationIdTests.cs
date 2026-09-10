using System.Net;
using System.Net.Http.Headers;
using ClosedXML.Excel;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.WebAPI.Correlation;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExcelETL.WebAPI.Tests.Correlation;

// The one thing worth proving with a real HTTP round trip: the correlation ID header survives
// BOTH the success path and the error path (via GlobalExceptionHandler/ExceptionHandlerMiddleware,
// which clears the response before writing its own body -- see the comment on the middleware
// registration in Program.cs for why OnStarting was needed rather than setting the header
// directly). Logging itself (LogContext.PushProperty) is not asserted here, per this project's
// own established convention that logging is an observability side effect, not TDD-driven
// business logic (Lot G1).
public class CorrelationIdTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ValidApiKey = "test-api-key-12345";

    private readonly WebApplicationFactory<Program> _factory;

    public CorrelationIdTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "CorrelationIdTests_" + Guid.NewGuid();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ApiKeyAuthentication:ApiKey", ValidApiKey);
            builder.UseSetting("Serilog:EnableMsSqlServerSink", "false");
            builder.UseSetting("Database:AutoMigrate", "false");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ExcelEtlDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ExcelEtlDbContext>>();
                services.AddDbContextFactory<ExcelEtlDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        });
    }

    [Fact]
    public async Task Get_WithoutCorrelationIdHeader_GeneratesOneAndEchoesItBack()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/health/ping");

        response.Headers.TryGetValues(CorrelationIdDefaults.HeaderName, out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Get_WithoutCorrelationIdHeader_GeneratesADifferentValuePerRequest()
    {
        var client = CreateAuthenticatedClient();

        var first = await client.GetAsync("/api/health/ping");
        var second = await client.GetAsync("/api/health/ping");

        var firstId = first.Headers.GetValues(CorrelationIdDefaults.HeaderName).Single();
        var secondId = second.Headers.GetValues(CorrelationIdDefaults.HeaderName).Single();
        firstId.Should().NotBe(secondId);
    }

    [Fact]
    public async Task Get_WithCorrelationIdHeader_EchoesTheSuppliedValueBack()
    {
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add(CorrelationIdDefaults.HeaderName, "legacy-call-38271");

        var response = await client.GetAsync("/api/health/ping");

        response.Headers.GetValues(CorrelationIdDefaults.HeaderName).Should().ContainSingle().Which.Should().Be("legacy-call-38271");
    }

    // Proves the header survives ExceptionHandlerMiddleware's own response.Clear() -- without the
    // OnStarting registration, this test fails (header absent) even though the ones above pass.
    // Deliberately exercises the ImportProfileNotFoundException -> GlobalExceptionHandler path
    // (a real thrown exception, not a plain BadRequest(...) returned from the controller action
    // like Lot 036.1's own missing-field checks) -- that's the actual code path whose
    // response.Clear() risked wiping the header before this middleware was added.
    [Fact]
    public async Task Post_WithUnknownImportProfileId_StillEchoesCorrelationIdOnTheExceptionHandlerResponse()
    {
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add(CorrelationIdDefaults.HeaderName, "legacy-call-error-path");

        using var content = new MultipartFormDataContent
        {
            { new StringContent(Guid.NewGuid().ToString()), "ImportProfileId" },
            { new StringContent(Guid.NewGuid().ToString()), "ExportProfileId" },
            { new ByteArrayContent(BuildMinimalValidXlsx()) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } }, "File", "source.xlsx" }
        };

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.GetValues(CorrelationIdDefaults.HeaderName).Should().ContainSingle().Which.Should().Be("legacy-call-error-path");
    }

    // A syntactically valid, otherwise-empty .xlsx -- just enough for ClosedXmlWorkbookReader's own
    // construction to succeed, so the request reaches profile resolution (ImportProfileNotFoundException,
    // a real thrown exception handled by GlobalExceptionHandler) rather than failing earlier on
    // OxoController's own InvalidExcelFileFormat check.
    private static byte[] BuildMinimalValidXlsx()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Sheet1");
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);
        return client;
    }
}
