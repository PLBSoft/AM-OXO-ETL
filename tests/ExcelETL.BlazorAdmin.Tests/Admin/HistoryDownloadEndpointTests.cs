using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ExcelETL.Domain.Entities;
using ExcelETL.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Admin;

public class HistoryDownloadEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _databaseName = "HistoryDownloadTests_" + Guid.NewGuid();
    private readonly string _archiveDirectory =
        Path.Combine(Path.GetTempPath(), "ExcelEtlDownloadTests_" + Guid.NewGuid());

    private readonly WebApplicationFactory<Program> _baseFactory;

    public HistoryDownloadEndpointTests(WebApplicationFactory<Program> factory)
    {
        Directory.CreateDirectory(_archiveDirectory);

        _baseFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Serilog:EnableMsSqlServerSink", "false");
            builder.UseSetting("IdentitySeeding:Enabled", "false");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ExcelEtlDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ExcelEtlDbContext>>();
                services.AddDbContextFactory<ExcelEtlDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        });
    }

    private WebApplicationFactory<Program> CreateAuthenticatedFactory() =>
        _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                // AddIdentity() already configured Identity.Application as the default scheme;
                // PostConfigure guarantees this override wins regardless of registration order.
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultSignInScheme = TestAuthHandler.SchemeName;
                });
            });
        });

    private static async Task<Guid> SeedHistoryAsync(WebApplicationFactory<Program> factory, string? storedFilePath)
    {
        using var scope = factory.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ExcelEtlDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var history = new ExtractionHistory(DateTimeOffset.UtcNow, "invoice.xlsx");
        if (storedFilePath is not null)
        {
            history.MarkCompleted(storedFilePath);
        }

        dbContext.ExtractionHistories.Add(history);
        await dbContext.SaveChangesAsync();

        return history.Id;
    }

    [Fact]
    public async Task Download_WhenUnauthenticated_RedirectsToLogin()
    {
        var client = _baseFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/history/{Guid.NewGuid()}/download");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Download_WithUnknownId_ReturnsNotFound()
    {
        var factory = CreateAuthenticatedFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/history/{Guid.NewGuid()}/download");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_WhenFileMissingFromDisk_ReturnsNotFound()
    {
        var factory = CreateAuthenticatedFactory();
        var missingPath = Path.Combine(_archiveDirectory, "missing.xlsx");
        var id = await SeedHistoryAsync(factory, missingPath);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/history/{id}/download");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_WithCompletedEntryAndExistingFile_ReturnsFileBytes()
    {
        var factory = CreateAuthenticatedFactory();
        var filePath = Path.Combine(_archiveDirectory, "invoice-processed.xlsx");
        var expectedBytes = new byte[] { 1, 2, 3, 4 };
        await File.WriteAllBytesAsync(filePath, expectedBytes);
        var id = await SeedHistoryAsync(factory, filePath);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/history/{id}/download");
        var actualBytes = await response.Content.ReadAsByteArrayAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        actualBytes.Should().BeEquivalentTo(expectedBytes);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    public void Dispose()
    {
        if (Directory.Exists(_archiveDirectory))
        {
            Directory.Delete(_archiveDirectory, recursive: true);
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-admin")], SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
