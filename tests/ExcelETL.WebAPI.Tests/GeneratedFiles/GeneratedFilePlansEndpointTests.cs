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

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, 091.7, D14): the plan pages printed to
// PDF, listed and downloaded by the M2M caller.
public class GeneratedFilePlansEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ValidApiKey = "test-api-key-12345";
    private const string Paysage = "PLANS ISOLEMENT (Paysage)";
    private const string Platinage = "PLANS PLATINAGE & JOINTS";

    private readonly string _archiveRoot =
        Path.Combine(Path.GetTempPath(), "GeneratedFilePlansEndpointTests_" + Guid.NewGuid());
    private readonly WebApplicationFactory<Program> _factory;

    public GeneratedFilePlansEndpointTests(WebApplicationFactory<Program> factory)
    {
        Directory.CreateDirectory(_archiveRoot);
        var databaseName = "GeneratedFilePlansEndpointTests_" + Guid.NewGuid();

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

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);
        return client;
    }

    private string WriteFile(string fileName, byte[] content)
    {
        File.WriteAllBytes(Path.Combine(_archiveRoot, fileName), content);
        return fileName;
    }

    private async Task<GeneratedFileRecord> SeedAsync(params GeneratedFilePlan[] plans)
    {
        var record = new GeneratedFileRecord(
            Guid.NewGuid(), DateTime.UtcNow, "38-C7401", "source.xlsx", WriteFile($"{Guid.NewGuid()}-s.xlsx", [1]),
            "MAD_38-C7401.xlsx", WriteFile($"{Guid.NewGuid()}-t.xlsx", [2]), Guid.NewGuid(), Guid.NewGuid(),
            GeneratedFileArchiveStatus.Success, plans: plans);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IGeneratedFileArchiveStore>().SaveAsync(record);
        return record;
    }

    private GeneratedFilePlan Plan(string sheetName, int pageNumber, byte[] content)
    {
        var fileName = $"MAD_38-C7401_{sheetName}_p{pageNumber}.pdf";
        return new GeneratedFilePlan(sheetName, pageNumber, fileName, WriteFile($"{Guid.NewGuid()}-plan.pdf", content), content.Length);
    }

    [Fact]
    public async Task ListPlans_ReturnsEveryPage_InAStableOrder_WithoutServerPaths()
    {
        var client = CreateAuthenticatedClient();
        var record = await SeedAsync(Plan(Platinage, 1, [9]), Plan(Paysage, 2, [7, 7]), Plan(Paysage, 1, [5]));

        var response = await client.GetAsync($"/api/generated-files/{record.Id}/plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plans = await response.Content.ReadFromJsonAsync<List<GeneratedFilePlanResponse>>();
        plans!.Select(p => (p.Index, p.SheetName, p.PageNumber)).Should().Equal(
            (0, Paysage, 1), (1, Paysage, 2), (2, Platinage, 1));
        plans[1].FileName.Should().Be($"MAD_38-C7401_{Paysage}_p2.pdf");
        plans[1].SizeBytes.Should().Be(2);
        plans.Should().OnlyContain(p => p.Available);
        plans[2].DownloadUrl.Should().Be($"/api/generated-files/{record.Id}/plans/2");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("FilePath");
    }

    [Fact]
    public async Task GetById_IncludesTheSamePlanList()
    {
        var client = CreateAuthenticatedClient();
        var record = await SeedAsync(Plan(Paysage, 1, [5]));

        var summary = await client.GetFromJsonAsync<GeneratedFileSummaryResponse>($"/api/generated-files/{record.Id}");

        summary!.Plans.Should().ContainSingle().Which.DownloadUrl.Should().Be($"/api/generated-files/{record.Id}/plans/0");
    }

    [Fact]
    public async Task ListPlans_RecordWithoutPlans_ReturnsAnEmptyList()
    {
        var client = CreateAuthenticatedClient();
        var record = await SeedAsync();

        var plans = await client.GetFromJsonAsync<List<GeneratedFilePlanResponse>>($"/api/generated-files/{record.Id}/plans");

        plans.Should().BeEmpty();
    }

    [Fact]
    public async Task ListPlans_UnknownRecord_Returns404()
    {
        var response = await CreateAuthenticatedClient().GetAsync($"/api/generated-files/{Guid.NewGuid()}/plans");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DownloadPlan_ReturnsThePdf_WithItsFileName()
    {
        var client = CreateAuthenticatedClient();
        var record = await SeedAsync(Plan(Paysage, 1, [5]), Plan(Platinage, 1, [1, 2, 3]));

        var response = await client.GetAsync($"/api/generated-files/{record.Id}/plans/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be($"MAD_38-C7401_{Platinage}_p1.pdf");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(1, 2, 3);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task DownloadPlan_UnknownIndex_Returns404(int index)
    {
        var client = CreateAuthenticatedClient();
        var record = await SeedAsync(Plan(Paysage, 1, [5]));

        var response = await client.GetAsync($"/api/generated-files/{record.Id}/plans/{index}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DownloadPlan_AfterThePurge_Returns404_AndTheListSaysUnavailable()
    {
        var client = CreateAuthenticatedClient();
        var plan = Plan(Paysage, 1, [5]);
        var record = await SeedAsync(plan);
        File.Delete(Path.Combine(_archiveRoot, plan.FilePath));
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IGeneratedFileArchiveStore>()
                .MarkFilesPurgedAsync(record.Id, DateTime.UtcNow);
        }

        var download = await client.GetAsync($"/api/generated-files/{record.Id}/plans/0");
        var plans = await client.GetFromJsonAsync<List<GeneratedFilePlanResponse>>($"/api/generated-files/{record.Id}/plans");

        download.StatusCode.Should().Be(HttpStatusCode.NotFound);
        plans!.Single().Available.Should().BeFalse();
    }

    [Theory]
    [InlineData("plans")]
    [InlineData("plans/0")]
    public async Task PlanRoutes_WithoutApiKey_ReturnUnauthorized(string segment)
    {
        var response = await _factory.CreateClient().GetAsync($"/api/generated-files/{Guid.NewGuid()}/{segment}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
