using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Seeding;
using ExcelETL.WebAPI.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using PdfSharp.Pdf.IO;
using Xunit;

namespace ExcelETL.WebAPI.Tests.Oxo;

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md): the whole chain through a real HTTP
// request -- standard profiles, real file, real LibreOffice -- down to the plan PDFs the legacy app reads.
// Needs LibreOffice installed, hence the category.
[Trait("Category", "LibreOffice")]
public sealed class OxoProcessPlansEndToEndTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ValidApiKey = "test-api-key-12345";

    private readonly string _archiveRoot = Path.Combine(Path.GetTempPath(), "OxoProcessPlansEndToEndTests_" + Guid.NewGuid());
    private readonly WebApplicationFactory<Program> _factory;

    public OxoProcessPlansEndToEndTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "OxoProcessPlansEndToEndTests_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ApiKeyAuthentication:ApiKey", ValidApiKey);
            builder.UseSetting("GeneratedFilesArchive:RootPath", _archiveRoot);
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

    public void Dispose()
    {
        if (Directory.Exists(_archiveRoot))
        {
            Directory.Delete(_archiveRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Process_G6306B_WithTheStandardProfiles_ArchivesOnePdfPerPlanPage()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            await new DefaultProfileSeeder(
                scope.ServiceProvider.GetRequiredService<IImportProfileStore>(),
                scope.ServiceProvider.GetRequiredService<IExportProfileStore>(),
                NullLogger<DefaultProfileSeeder>.Instance).SeedAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);
        await using var file = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx"));
        var fileContent = new StreamContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var content = new MultipartFormDataContent
        {
            { new StringContent(DefaultProfileSeeder.ImportProfileId.ToString()), "ImportProfileId" },
            { new StringContent(DefaultProfileSeeder.ExportProfileId.ToString()), "ExportProfileId" },
            { fileContent, "File", "G6306B.xlsx" },
        };

        var response = await client.PostAsync("/api/oxo/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var recordId = response.Headers.GetValues("X-Generated-File-Id").Single();
        var plans = await client.GetFromJsonAsync<List<GeneratedFilePlanResponse>>($"/api/generated-files/{recordId}/plans");
        plans!.Select(p => (p.SheetName, p.PageNumber)).Should().Equal(
            ("PLANS ISOLEMENT (Paysage)", 1), ("PLANS PLATINAGE & JOINTS", 1), ("PLANS PLATINAGE & JOINTS", 2));

        foreach (var plan in plans)
        {
            var pdf = await client.GetByteArrayAsync(plan.DownloadUrl);
            using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
            document.PageCount.Should().Be(1);
        }
    }

    private static string FixturePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Fixtures")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Could not locate the tests/Fixtures directory.")
            : Path.Combine(directory.FullName, "Fixtures", fileName);
    }
}
