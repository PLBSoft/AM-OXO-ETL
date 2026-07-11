using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClosedXML.Excel;
using ExcelETL.Application.Extraction;
using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Enums;
using ExcelETL.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExcelETL.WebAPI.Tests.Excel;

public class ExcelProcessEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ValidApiKey = "test-api-key-12345";
    private static readonly string[] SheetNames = ["Summary", "Details", "Totals", "Notes"];

    private readonly string _archiveDirectory = Path.Combine(Path.GetTempPath(), "ExcelEtlE2E_" + Guid.NewGuid());
    private readonly WebApplicationFactory<Program> _factory;

    public ExcelProcessEndpointTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "ExcelProcessTests_" + Guid.NewGuid();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ApiKeyAuthentication:ApiKey", ValidApiKey);
            builder.UseSetting("FileStorage:ArchiveDirectory", _archiveDirectory);
            builder.UseSetting("Serilog:EnableMsSqlServerSink", "false");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ExcelEtlDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ExcelEtlDbContext>>();
                services.AddDbContextFactory<ExcelEtlDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        });
    }

    [Fact]
    public async Task Process_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        using var content = new MultipartFormDataContent();
        var response = await client.PostAsync("/api/excel/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Process_WithUnknownExtractionConfigId_ReturnsNotFound()
    {
        var client = CreateAuthenticatedClient();
        using var sourceWorkbook = BuildSourceWorkbook();
        using var content = BuildMultipartContent(Guid.NewGuid(), sourceWorkbook);

        var response = await client.PostAsync("/api/excel/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Process_WithUnknownExtractionConfigId_ReturnsEnglishDetailByDefault()
    {
        var client = CreateAuthenticatedClient();
        var configId = Guid.NewGuid();
        using var sourceWorkbook = BuildSourceWorkbook();
        using var content = BuildMultipartContent(configId, sourceWorkbook);

        var response = await client.PostAsync("/api/excel/process", content);
        var detail = await ReadProblemDetailAsync(response);

        detail.Should().Be($"Extraction config '{configId}' was not found.");
    }

    [Fact]
    public async Task Process_WithUnknownExtractionConfigId_AndFrenchAcceptLanguage_ReturnsFrenchDetail()
    {
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr-FR"));
        var configId = Guid.NewGuid();
        using var sourceWorkbook = BuildSourceWorkbook();
        using var content = BuildMultipartContent(configId, sourceWorkbook);

        var response = await client.PostAsync("/api/excel/process", content);
        var detail = await ReadProblemDetailAsync(response);

        detail.Should().Be($"La configuration d'extraction '{configId}' est introuvable.");
    }

    [Fact]
    public async Task Process_WithEmptyFile_ReturnsBadRequest()
    {
        var client = CreateAuthenticatedClient();
        var configId = await SeedExtractionConfigAsync();

        using var content = new MultipartFormDataContent
        {
            { new StringContent(configId.ToString()), "ExtractionConfigId" },
            { new ByteArrayContent([]), "File", "empty.xlsx" }
        };

        var response = await client.PostAsync("/api/excel/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Process_WithEmptyFile_AndFrenchAcceptLanguage_ReturnsFrenchDetail()
    {
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr-FR"));
        var configId = await SeedExtractionConfigAsync();

        using var content = new MultipartFormDataContent
        {
            { new StringContent(configId.ToString()), "ExtractionConfigId" },
            { new ByteArrayContent([]), "File", "empty.xlsx" }
        };

        var response = await client.PostAsync("/api/excel/process", content);
        var detail = await ReadProblemDetailAsync(response);

        detail.Should().Be("Un fichier .xlsx non vide doit être envoyé.");
    }

    [Fact]
    public async Task Process_WithWorkbookMissingConfiguredSheet_ReturnsBadRequestWithLocalizedDetail()
    {
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr-FR"));
        var configId = await SeedExtractionConfigAsync();

        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("NotTheRightSheet").Cell("B2").Value = "irrelevant";
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        using var content = BuildMultipartContent(configId, stream);

        var response = await client.PostAsync("/api/excel/process", content);
        var detail = await ReadProblemDetailAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        detail.Should().Be("La feuille 'Summary' est introuvable dans le classeur envoyé.");
    }

    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("detail").GetString();
    }

    [Fact]
    public async Task Process_WithValidRequest_ReturnsGeneratedWorkbookWithFourSheets()
    {
        var client = CreateAuthenticatedClient();
        var configId = await SeedExtractionConfigAsync();
        using var sourceWorkbook = BuildSourceWorkbook();
        using var content = BuildMultipartContent(configId, sourceWorkbook);

        var response = await client.PostAsync("/api/excel/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        workbook.Worksheets.Count.Should().Be(4);
        workbook.Worksheet("Summary").Cell(2, 2).GetString().Should().Be("Acme Corp");

        Directory.Exists(_archiveDirectory).Should().BeTrue();
        Directory.GetFiles(_archiveDirectory, "*.xlsx").Should().ContainSingle();
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);
        return client;
    }

    private async Task<Guid> SeedExtractionConfigAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IExtractionConfigRepository>();

        var config = new ExtractionConfig("Purchase Order Template");
        for (var i = 0; i < SheetNames.Length; i++)
        {
            var sheet = new SheetConfig(SheetNames[i], sheetIndex: i);
            sheet.AddCellMapping(new CellMapping("B2", "SupplierName", CellDataType.Text));
            config.AddSheet(sheet);
        }

        await repository.AddAsync(config);

        return config.Id;
    }

    private static MemoryStream BuildSourceWorkbook()
    {
        using var workbook = new XLWorkbook();
        foreach (var name in SheetNames)
        {
            workbook.Worksheets.Add(name).Cell("B2").Value = "Acme Corp";
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static MultipartFormDataContent BuildMultipartContent(Guid configId, Stream fileStream)
    {
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        return new MultipartFormDataContent
        {
            { new StringContent(configId.ToString()), "ExtractionConfigId" },
            { fileContent, "File", "source.xlsx" }
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_archiveDirectory))
        {
            Directory.Delete(_archiveDirectory, recursive: true);
        }
    }
}
