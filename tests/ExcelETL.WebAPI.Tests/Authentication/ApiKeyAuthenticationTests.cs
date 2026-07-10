using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ExcelETL.WebAPI.Tests.Authentication;

public class ApiKeyAuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ValidApiKey = "test-api-key-12345";

    private readonly WebApplicationFactory<Program> _factory;

    public ApiKeyAuthenticationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ApiKeyAuthentication:ApiKey", ValidApiKey));
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
