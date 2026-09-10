using System.Net;
using System.Net.Http.Json;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
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

namespace ExcelETL.WebAPI.Tests.Profiles;

// Read-only counterpart to OxoController's own fixture, so the legacy MVC5 upload screen can
// populate its Import/Export profile dropdowns instead of guessing a fixed profile Id.
public class ProfileListEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ValidApiKey = "test-api-key-12345";

    private readonly WebApplicationFactory<Program> _factory;

    public ProfileListEndpointsTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "ProfileListEndpointsTests_" + Guid.NewGuid();

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
    public async Task GetImportProfiles_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/import-profiles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetExportProfiles_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/export-profiles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetImportProfiles_WithNoProfilesSeeded_ReturnsEmptyArray()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/import-profiles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ProfileSummaryResponse>>();
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExportProfiles_WithNoProfilesSeeded_ReturnsEmptyArray()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/export-profiles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ProfileSummaryResponse>>();
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetImportProfiles_WithSeededProfiles_ReturnsIdAndNameSortedByName()
    {
        var client = CreateAuthenticatedClient();
        var zProfile = CreateImportProfile("Zzz profil");
        var aProfile = CreateImportProfile("Aaa profil");
        await SeedAsync(store => store.SaveAsync(zProfile));
        await SeedAsync(store => store.SaveAsync(aProfile));

        var response = await client.GetAsync("/api/import-profiles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ProfileSummaryResponse>>();
        body.Should().BeEquivalentTo(
            [
                new ProfileSummaryResponse(aProfile.Id, "Aaa profil"),
                new ProfileSummaryResponse(zProfile.Id, "Zzz profil")
            ],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetExportProfiles_WithSeededProfiles_ReturnsIdAndNameSortedByName()
    {
        var client = CreateAuthenticatedClient();
        var zProfile = CreateExportProfile("Zzz profil");
        var aProfile = CreateExportProfile("Aaa profil");
        await SeedAsync(store => store.SaveAsync(zProfile));
        await SeedAsync(store => store.SaveAsync(aProfile));

        var response = await client.GetAsync("/api/export-profiles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ProfileSummaryResponse>>();
        body.Should().BeEquivalentTo(
            [
                new ProfileSummaryResponse(aProfile.Id, "Aaa profil"),
                new ProfileSummaryResponse(zProfile.Id, "Zzz profil")
            ],
            options => options.WithStrictOrdering());
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);
        return client;
    }

    private async Task SeedAsync(Func<IImportProfileStore, Task> save)
    {
        using var scope = _factory.Services.CreateScope();
        await save(scope.ServiceProvider.GetRequiredService<IImportProfileStore>());
    }

    private async Task SeedAsync(Func<IExportProfileStore, Task> save)
    {
        using var scope = _factory.Services.CreateScope();
        await save(scope.ServiceProvider.GetRequiredService<IExportProfileStore>());
    }

    private static ImportProfile CreateImportProfile(string name) => new(
        name, "MAD TRAVAUX",
        [], [],
        [
            new SheetExtractionRule(
                "Sheet1",
                new RepeatingBlockLocator("Sheet1", 1, 1, "Field1", [new BlockFieldDefinition("Field1", "A", 0, 0)]),
                [], [], [], [])
        ]);

    private static ExportProfile CreateExportProfile(string name) => new(
        name,
        [
            new SheetGenerationRule(
                "Sheet1", PivotSource.Equipement,
                [new ColumnDefinition("Col1", PivotFieldRef.EquipementRepere)], [], [])
        ]);
}
