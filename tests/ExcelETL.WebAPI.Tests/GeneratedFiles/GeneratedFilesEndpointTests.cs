using System.Net;
using System.Net.Http.Json;
using ExcelETL.Application.Archiving;
using ExcelETL.Domain.Archiving;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.WebAPI.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExcelETL.WebAPI.Tests.GeneratedFiles;

public class GeneratedFilesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ValidApiKey = "test-api-key-12345";

    private readonly WebApplicationFactory<Program> _factory;

    public GeneratedFilesEndpointTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "GeneratedFilesEndpointTests_" + Guid.NewGuid();

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
    public async Task Search_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/generated-files");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/generated-files/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_WithNoRecords_ReturnsEmptyArray()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/generated-files");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GeneratedFileSummaryResponse>>();
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_WithSeededRecords_ReturnsMetadataOnly_SortedByGeneratedAtDescending()
    {
        var client = CreateAuthenticatedClient();
        var older = BuildRecord(equipementRepere: "38-C7401", generatedAtUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = BuildRecord(equipementRepere: "38-D8570", generatedAtUtc: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));
        await SeedAsync(older);
        await SeedAsync(newer);

        var response = await client.GetAsync("/api/generated-files");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GeneratedFileSummaryResponse>>();
        body.Should().HaveCount(2);
        body![0].Id.Should().Be(newer.Id);
        body[1].Id.Should().Be(older.Id);
        // SourceFilePath/TargetFilePath are server-local filesystem paths -- must never leak into
        // the HTTP response, only the raw JSON body can prove this (a typed DTO simply has no
        // property for them, which wouldn't fail even if the controller leaked them).
        var rawBody = await (await client.GetAsync("/api/generated-files")).Content.ReadAsStringAsync();
        rawBody.Should().NotContain("FilePath");
    }

    [Fact]
    public async Task Search_WithEquipementRepereFilter_ReturnsOnlyMatchingRecords()
    {
        var client = CreateAuthenticatedClient();
        var matching = BuildRecord(equipementRepere: "38-C7401");
        var nonMatching = BuildRecord(equipementRepere: "38-D8570");
        await SeedAsync(matching);
        await SeedAsync(nonMatching);

        var response = await client.GetAsync("/api/generated-files?equipementRepere=C7401");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GeneratedFileSummaryResponse>>();
        body.Should().ContainSingle().Which.Id.Should().Be(matching.Id);
    }

    [Fact]
    public async Task GetById_WithExistingRecord_ReturnsItsMetadata()
    {
        var client = CreateAuthenticatedClient();
        var record = BuildRecord(equipementRepere: "38-C7401", status: GeneratedFileArchiveStatus.NonBlockingWarning);
        await SeedAsync(record);

        var response = await client.GetAsync($"/api/generated-files/{record.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GeneratedFileSummaryResponse>();
        body!.Id.Should().Be(record.Id);
        body.EquipementRepere.Should().Be("38-C7401");
        body.SourceFileName.Should().Be(record.SourceFileName);
        body.TargetFileName.Should().Be(record.TargetFileName);
        body.ImportProfileId.Should().Be(record.ImportProfileId);
        body.ExportProfileId.Should().Be(record.ExportProfileId);
        body.Status.Should().Be("NonBlockingWarning");
    }

    [Fact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/generated-files/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_WithRejectedRecordCarryingNullTargetFields_ReturnsNullTargetFileName()
    {
        var client = CreateAuthenticatedClient();
        var record = new GeneratedFileRecord(
            Guid.NewGuid(), DateTime.UtcNow, equipementRepere: null,
            sourceFileName: "rejected-source.xlsx", sourceFilePath: @"C:\archive\rejected-source.xlsx",
            targetFileName: null, targetFilePath: null,
            importProfileId: Guid.NewGuid(), exportProfileId: Guid.NewGuid(),
            status: GeneratedFileArchiveStatus.Rejected);
        await SeedAsync(record);

        var response = await client.GetAsync($"/api/generated-files/{record.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GeneratedFileSummaryResponse>();
        body!.TargetFileName.Should().BeNull();
        body.EquipementRepere.Should().BeNull();
        body.Status.Should().Be("Rejected");
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);
        return client;
    }

    private async Task SeedAsync(GeneratedFileRecord record)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IGeneratedFileArchiveStore>();
        await store.SaveAsync(record);
    }

    private static GeneratedFileRecord BuildRecord(
        string? equipementRepere = "38-C7401",
        DateTime? generatedAtUtc = null,
        GeneratedFileArchiveStatus status = GeneratedFileArchiveStatus.Success) => new(
        Guid.NewGuid(),
        generatedAtUtc ?? DateTime.UtcNow,
        equipementRepere,
        sourceFileName: "source.xlsx",
        sourceFilePath: @"C:\archive\source.xlsx",
        targetFileName: "MAD_38-C7401_20260910120000.xlsx",
        targetFilePath: @"C:\archive\MAD_38-C7401_20260910120000.xlsx",
        importProfileId: Guid.NewGuid(),
        exportProfileId: Guid.NewGuid(),
        status: status);
}
