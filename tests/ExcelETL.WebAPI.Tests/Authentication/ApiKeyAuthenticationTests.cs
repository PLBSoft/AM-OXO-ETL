using System.Net;
using ExcelETL.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExcelETL.WebAPI.Tests.Authentication;

public class ApiKeyAuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ValidApiKey = "test-api-key-12345";

    private readonly WebApplicationFactory<Program> _factory;

    public ApiKeyAuthenticationTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "ApiKeyAuthenticationTests_" + Guid.NewGuid();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ApiKeyAuthentication:ApiKey", ValidApiKey);
            builder.UseSetting("Serilog:EnableMsSqlServerSink", "false");
            builder.UseSetting("Database:AutoMigrate", "false");

            // Get_WithValidApiKeyHeader_ReturnsOk reaches HealthController, which now does a real
            // DB connectivity check -- swapped to InMemory so this test never attempts a real SQL
            // Server connection (same convention as OxoProcessEndpointTests/HealthEndpointTests).
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ExcelEtlDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ExcelEtlDbContext>>();
                services.AddDbContextFactory<ExcelEtlDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
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
    public async Task Get_WithInvalidApiKeyHeader_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WithValidApiKeyHeader_ReturnsOk()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
