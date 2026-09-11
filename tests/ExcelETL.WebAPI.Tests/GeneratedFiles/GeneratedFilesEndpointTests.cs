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

public class GeneratedFilesEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ValidApiKey = "test-api-key-12345";

    private readonly string _archiveRoot =
        Path.Combine(Path.GetTempPath(), "GeneratedFilesEndpointTests_" + Guid.NewGuid());
    private readonly WebApplicationFactory<Program> _factory;

    public GeneratedFilesEndpointTests(WebApplicationFactory<Program> factory)
    {
        Directory.CreateDirectory(_archiveRoot);
        var databaseName = "GeneratedFilesEndpointTests_" + Guid.NewGuid();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ApiKeyAuthentication:ApiKey", ValidApiKey);
            builder.UseSetting("Serilog:EnableMsSqlServerSink", "false");
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("GeneratedFilesArchive:RootPath", _archiveRoot);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ExcelEtlDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ExcelEtlDbContext>>();
                services.AddDbContextFactory<ExcelEtlDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_archiveRoot))
        {
            Directory.Delete(_archiveRoot, recursive: true);
        }
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

    [Theory]
    [InlineData("source")]
    [InlineData("target")]
    public async Task Download_WithoutApiKey_ReturnsUnauthorized(string segment)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/generated-files/{Guid.NewGuid()}/{segment}");

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
        var older = await SeedRecordAsync(equipementRepere: "38-C7401", generatedAtUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = await SeedRecordAsync(equipementRepere: "38-D8570", generatedAtUtc: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));

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
        var matching = await SeedRecordAsync(equipementRepere: "38-C7401");
        await SeedRecordAsync(equipementRepere: "38-D8570");

        var response = await client.GetAsync("/api/generated-files?equipementRepere=C7401");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GeneratedFileSummaryResponse>>();
        body.Should().ContainSingle().Which.Id.Should().Be(matching.Id);
    }

    [Fact]
    public async Task GetById_WithExistingRecord_ReturnsMetadataAndDownloadUrls()
    {
        var client = CreateAuthenticatedClient();
        var record = await SeedRecordAsync(equipementRepere: "38-C7401", status: GeneratedFileArchiveStatus.NonBlockingWarning);

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
        body.SourceDownloadUrl.Should().Be($"/api/generated-files/{record.Id}/source");
        body.TargetDownloadUrl.Should().Be($"/api/generated-files/{record.Id}/target");
        body.Username.Should().BeNull();
    }

    [Fact]
    public async Task GetById_WithUsernameSupplied_ReturnsIt()
    {
        var client = CreateAuthenticatedClient();
        var record = await SeedRecordAsync(username: "jdupont");

        var response = await client.GetAsync($"/api/generated-files/{record.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GeneratedFileSummaryResponse>();
        body!.Username.Should().Be("jdupont");
    }

    // -- Lot 071: element counts --

    [Fact]
    public async Task GetById_WithNonZeroElementCounts_ReturnsThemInTheBody()
    {
        var client = CreateAuthenticatedClient();
        var record = await SeedRecordAsync(isolementCount: 23, pointCount: 47, tacheMultipleCount: 98);

        var response = await client.GetAsync($"/api/generated-files/{record.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GeneratedFileSummaryResponse>();
        body!.IsolementCount.Should().Be(23);
        body.PointCount.Should().Be(47);
        body.TacheMultipleCount.Should().Be(98);
    }

    [Fact]
    public async Task GetById_WithRejectedRecord_ReturnsZeroElementCounts_NeverNull()
    {
        var client = CreateAuthenticatedClient();
        var record = new GeneratedFileRecord(
            Guid.NewGuid(), DateTime.UtcNow, equipementRepere: null,
            sourceFileName: "rejected-source.xlsx", sourceFilePath: WriteFile("rejected-source.xlsx"),
            targetFileName: null, targetFilePath: null,
            importProfileId: Guid.NewGuid(), exportProfileId: Guid.NewGuid(),
            status: GeneratedFileArchiveStatus.Rejected);
        await SeedAsync(record);

        var response = await client.GetAsync($"/api/generated-files/{record.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GeneratedFileSummaryResponse>();
        body!.IsolementCount.Should().Be(0);
        body.PointCount.Should().Be(0);
        body.TacheMultipleCount.Should().Be(0);
    }

    [Fact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/generated-files/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_WithRejectedRecordCarryingNullTargetFields_ReturnsNullTargetFileNameAndUrl()
    {
        var client = CreateAuthenticatedClient();
        var record = new GeneratedFileRecord(
            Guid.NewGuid(), DateTime.UtcNow, equipementRepere: null,
            sourceFileName: "rejected-source.xlsx", sourceFilePath: WriteFile("rejected-source.xlsx"),
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
        body.SourceDownloadUrl.Should().Be($"/api/generated-files/{record.Id}/source");
        body.TargetDownloadUrl.Should().BeNull();
    }

    [Fact]
    public async Task DownloadSource_WithExistingRecord_ReturnsFileBytesWithNameAndContentType()
    {
        var client = CreateAuthenticatedClient();
        var record = await SeedRecordAsync(sourceContent: "source-bytes"u8.ToArray());

        var response = await client.GetAsync($"/api/generated-files/{record.Id}/source");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        response.Content.Headers.ContentDisposition!.FileName.Should().Be(record.SourceFileName);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().Equal("source-bytes"u8.ToArray());
    }

    [Fact]
    public async Task DownloadTarget_WithExistingRecord_ReturnsFileBytesWithNameAndContentType()
    {
        var client = CreateAuthenticatedClient();
        var record = await SeedRecordAsync(targetContent: "target-bytes"u8.ToArray());

        var response = await client.GetAsync($"/api/generated-files/{record.Id}/target");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileName.Should().Be(record.TargetFileName);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().Equal("target-bytes"u8.ToArray());
    }

    [Theory]
    [InlineData("source")]
    [InlineData("target")]
    public async Task Download_WithUnknownId_ReturnsNotFound(string segment)
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/generated-files/{Guid.NewGuid()}/{segment}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DownloadTarget_WithRejectedRecord_ReturnsNotFound()
    {
        var client = CreateAuthenticatedClient();
        var record = new GeneratedFileRecord(
            Guid.NewGuid(), DateTime.UtcNow, equipementRepere: null,
            sourceFileName: "rejected-source.xlsx", sourceFilePath: WriteFile("rejected-source.xlsx"),
            targetFileName: null, targetFilePath: null,
            importProfileId: Guid.NewGuid(), exportProfileId: Guid.NewGuid(),
            status: GeneratedFileArchiveStatus.Rejected);
        await SeedAsync(record);

        var response = await client.GetAsync($"/api/generated-files/{record.Id}/target");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DownloadTarget_WhenFileMissingFromDisk_ReturnsNotFound_NotAServerError()
    {
        var client = CreateAuthenticatedClient();
        var record = await SeedRecordAsync();
        File.Delete(Path.Combine(_archiveRoot, record.TargetFileName!));

        var response = await client.GetAsync($"/api/generated-files/{record.Id}/target");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Regression test for the real production defect this lot fixed: a nested relative path (the
    // exact {yyyy}\{MM}\{file} shape FileSystemGeneratedFileWriter actually produces, not just a
    // bare filename) must still resolve correctly once joined with GeneratedFilesArchive:RootPath.
    [Fact]
    public async Task DownloadSource_WithNestedRelativePath_JoinsArchiveRootAndSucceeds()
    {
        var client = CreateAuthenticatedClient();
        var relativeDirectory = Path.Combine("2026", "09");
        Directory.CreateDirectory(Path.Combine(_archiveRoot, relativeDirectory));
        var relativePath = Path.Combine(relativeDirectory, "20260910-120000-000_source_source.xlsx");
        File.WriteAllBytes(Path.Combine(_archiveRoot, relativePath), "nested-source-bytes"u8.ToArray());

        var record = new GeneratedFileRecord(
            Guid.NewGuid(), DateTime.UtcNow, "38-C7401",
            sourceFileName: "source.xlsx", sourceFilePath: relativePath,
            targetFileName: null, targetFilePath: null,
            importProfileId: Guid.NewGuid(), exportProfileId: Guid.NewGuid(),
            status: GeneratedFileArchiveStatus.Rejected);
        await SeedAsync(record);

        var response = await client.GetAsync($"/api/generated-files/{record.Id}/source");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().Equal("nested-source-bytes"u8.ToArray());
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

    // Writes to _archiveRoot but returns the relative path only -- mirrors exactly what
    // IGeneratedFileWriter/FileSystemGeneratedFileWriter actually returns (relative to the
    // configured root, never absolute), so GeneratedFileRecord.SourceFilePath/TargetFilePath in
    // these tests carries the same shape the controller must handle in real production, not the
    // absolute path a naive test double would be tempted to use instead.
    private string WriteFile(string fileName, byte[]? content = null)
    {
        var path = Path.Combine(_archiveRoot, fileName);
        File.WriteAllBytes(path, content ?? [1, 2, 3]);
        return fileName;
    }

    private async Task<GeneratedFileRecord> SeedRecordAsync(
        string? equipementRepere = "38-C7401",
        DateTime? generatedAtUtc = null,
        GeneratedFileArchiveStatus status = GeneratedFileArchiveStatus.Success,
        byte[]? sourceContent = null,
        byte[]? targetContent = null,
        string? username = null,
        int isolementCount = 0,
        int pointCount = 0,
        int tacheMultipleCount = 0)
    {
        var id = Guid.NewGuid();
        var record = new GeneratedFileRecord(
            id,
            generatedAtUtc ?? DateTime.UtcNow,
            equipementRepere,
            sourceFileName: $"{id}-source.xlsx",
            sourceFilePath: WriteFile($"{id}-source.xlsx", sourceContent),
            targetFileName: $"{id}-target.xlsx",
            targetFilePath: WriteFile($"{id}-target.xlsx", targetContent),
            importProfileId: Guid.NewGuid(),
            exportProfileId: Guid.NewGuid(),
            status: status,
            username: username,
            isolementCount: isolementCount,
            pointCount: pointCount,
            tacheMultipleCount: tacheMultipleCount);
        await SeedAsync(record);
        return record;
    }
}
