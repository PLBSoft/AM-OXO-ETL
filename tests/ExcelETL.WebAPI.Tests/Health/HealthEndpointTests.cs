using System.Net;
using System.Net.Http.Json;
using ExcelETL.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExcelETL.WebAPI.Tests.Health;

// GET /api/health -- distinct from GET /api/health/ping (HealthPingTests), a richer diagnostic
// endpoint (API version + database reachability) added independently of the minimal ping contract
// already agreed with the legacy caller.
public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ValidApiKey = "test-api-key-12345";

    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ApiKeyAuthentication:ApiKey", ValidApiKey);
            builder.UseSetting("Serilog:EnableMsSqlServerSink", "false");
            builder.UseSetting("Database:AutoMigrate", "false");
        });
    }

    [Fact]
    public async Task Get_WithoutApiKeyHeader_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WithReachableDatabase_ReturnsHealthyWithVersionAndDatabaseHealthy()
    {
        var client = CreateAuthenticatedClientWithInMemoryDatabase();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body!.Status.Should().Be("Healthy");
        body.Database.Should().Be("Healthy");
        body.Version.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Get_WithUnreachableDatabase_ReturnsDegradedWithDatabaseUnhealthy_ButStillOk()
    {
        var client = CreateAuthenticatedClientWithThrowingDatabase();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body!.Status.Should().Be("Degraded");
        body.Database.Should().Be("Unhealthy");
        body.Version.Should().NotBeNullOrWhiteSpace();
    }

    private HttpClient CreateAuthenticatedClientWithInMemoryDatabase()
    {
        var databaseName = "HealthEndpointTests_" + Guid.NewGuid();
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ExcelEtlDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ExcelEtlDbContext>>();
                services.AddDbContextFactory<ExcelEtlDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);
        return client;
    }

    private HttpClient CreateAuthenticatedClientWithThrowingDatabase()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextFactory<ExcelEtlDbContext>>();
                services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(new ThrowingDbContextFactory());
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);
        return client;
    }

    // Stands in for a genuinely unreachable database -- simpler and faster than pointing the real
    // provider at an unreachable SQL Server and waiting out its own connection timeout.
    private sealed class ThrowingDbContextFactory : IDbContextFactory<ExcelEtlDbContext>
    {
        public ExcelEtlDbContext CreateDbContext() => throw new InvalidOperationException("Database unreachable.");

        public Task<ExcelEtlDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Database unreachable.");
    }

    private sealed record HealthResponse(string Status, string Version, string Database);
}
