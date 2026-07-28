using System.Net;
using System.Text.RegularExpressions;
using ExcelETL.Infrastructure.Identity;
using ExcelETL.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Account;

// Lot 052 (52.3): a refused authorization must land on a real "access denied" page, not the generic
// "Not Found" one -- the exact defect a real non-Admin account hit on 2026-07-28 (see the ticket
// document). Every assertion here goes through a real HTTP request against a real host, per the
// convention doc's §3: authorization is never provable by a bUnit render.
public class AccessDeniedHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string AccessDeniedUrl = "/Account/AccessDenied";
    private const string TestPassword = "TestP@ssw0rd!";

    private readonly WebApplicationFactory<Program> _factory;

    public AccessDeniedHttpTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "AccessDeniedHttpTests_" + Guid.NewGuid();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Serilog:EnableMsSqlServerSink", "false");
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("IdentitySeeding:Enabled", "false");
            builder.UseSetting("ProfileSeeding:Enabled", "false");
            builder.UseSetting("OxoApiTestClient:BaseUrl", "https://localhost:7088/");
            builder.UseSetting("OxoApiTestClient:ApiKey", "unused-in-tests");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ExcelEtlDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ExcelEtlDbContext>>();
                services.AddDbContextFactory<ExcelEtlDbContext>(
                    options => options.UseInMemoryDatabase(databaseName));

                services.RemoveAll<DbContextOptions<ApplicationIdentityDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationIdentityDbContext>>();
                services.AddDbContext<ApplicationIdentityDbContext>(
                    options => options.UseInMemoryDatabase(databaseName + "_identity"));
                services.AddDbContextFactory<ApplicationIdentityDbContext>(
                    options => options.UseInMemoryDatabase(databaseName + "_identity"),
                    lifetime: ServiceLifetime.Scoped);
            });
        });
    }

    [Fact]
    public async Task Get_WithoutAuthentication_ReturnsOkAndTheAccessDeniedMessage()
    {
        var client = CreateClient();

        var response = await client.GetAsync(AccessDeniedUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"access-denied-message\"");
    }

    [Fact]
    public async Task Get_WhileAuthenticated_ReturnsOkAndTheAccessDeniedMessage()
    {
        await CreateUserAsync("plain_user");
        var client = await CreateSignedInClientAsync("plain_user");

        var response = await client.GetAsync(AccessDeniedUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"access-denied-message\"");
    }

    // The test that distinguishes the two outcomes -- written before AccessDenied.razor exists, per
    // the ticket's own explicit instruction, and confirmed red beforehand: today a non-Admin account
    // hitting a refused Admin-only page is bounced to a page that does not exist yet, so it renders
    // as "Not Found", not as an access-denied message.
    [Fact]
    public async Task NonAdminAccount_DeniedAccessToUsersPage_LandsOnAccessDeniedPage_NotOnNotFoundPage()
    {
        await CreateUserAsync("plain_user");
        var client = await CreateSignedInClientAsync("plain_user");

        var redirect = await client.GetAsync("/users");
        redirect.StatusCode.Should().Be(HttpStatusCode.Redirect);
        redirect.Headers.Location!.AbsolutePath.Should().Be(AccessDeniedUrl);

        var followUp = await client.GetAsync(redirect.Headers.Location);
        followUp.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await followUp.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"access-denied-message\"");
        html.Should().NotContain("id=\"not-found-message\"");
    }

    // The return link must point somewhere an authenticated, role-less account can actually reach --
    // never back to a page that would refuse it again (convention doc §5's redirect-loop trap).
    [Fact]
    public async Task ReturnLink_PointsToARouteAccessibleToAnAuthenticatedAccountWithoutARole()
    {
        var client = CreateClient();

        var response = await client.GetAsync(AccessDeniedUrl);

        var html = await response.Content.ReadAsStringAsync();
        var hrefMatch = Regex.Match(html, "id=\"access-denied-back-link\"[^>]*href=\"([^\"]*)\"");
        hrefMatch.Success.Should().BeTrue("the page must render a stable, findable back link");
        hrefMatch.Groups[1].Value.Should().Be("/");
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task CreateUserAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName + "@example.com",
            FirstName = "Plain",
            LastName = "User",
        };

        var result = await userManager.CreateAsync(user, TestPassword);
        result.Succeeded.Should().BeTrue(
            "the test user must exist: " + string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    private async Task<HttpClient> CreateSignedInClientAsync(string userName)
    {
        var client = CreateClient();

        var loginPage = await client.GetAsync("/Account/Login");
        var fields = ReadHiddenFields(await loginPage.Content.ReadAsStringAsync());
        fields["Input.UserName"] = userName;
        fields["Input.Password"] = TestPassword;
        fields["Input.RememberMe"] = "false";

        var signIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(fields));
        signIn.StatusCode.Should().Be(HttpStatusCode.Redirect, "sign-in must succeed for this test to mean anything");
        return client;
    }

    private static Dictionary<string, string> ReadHiddenFields(string html)
    {
        var fields = new Dictionary<string, string>();
        foreach (Match input in Regex.Matches(html, "<input[^>]*type=\"hidden\"[^>]*>"))
        {
            var name = Regex.Match(input.Value, "name=\"([^\"]*)\"").Groups[1].Value;
            if (name.Length == 0)
            {
                continue;
            }

            fields[name] = WebUtility.HtmlDecode(Regex.Match(input.Value, "value=\"([^\"]*)\"").Groups[1].Value);
        }

        return fields;
    }
}
