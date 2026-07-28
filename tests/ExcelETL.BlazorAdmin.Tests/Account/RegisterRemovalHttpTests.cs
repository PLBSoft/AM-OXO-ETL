using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ExcelETL.Infrastructure.Identity;
using ExcelETL.Infrastructure.Persistence;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Account;

// Lot 051 (51.1): the central test of the lot. Public self-registration is removed as a security
// decision (see docs/tickets/tickets-tdd-lot-051-retrait-inscription-publique.md) -- proven here via
// a real HTTP request, not a bUnit render, per the Lot 049 lesson that bUnit never proves a route is
// (un)reachable. This test must be red before Register.razor is deleted.
public class RegisterRemovalHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RegisterRemovalHttpTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "RegisterRemovalHttpTests_" + Guid.NewGuid();

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

    // 51.0/51.1 correction, verified empirically rather than assumed: the ticket's own hypothesis was
    // that an unauthenticated request would reach the Router's NotFoundPage (404, URL preserved, per
    // Lot 049's UseStatusCodePagesWithReExecute mechanism). Measured instead: Program.cs's global
    // AuthorizationOptions.FallbackPolicy (RequireAuthenticatedUser) gates MapRazorComponents<App>()'s
    // catch-all endpoint itself -- an unauthenticated request never reaches routing/the Router at all,
    // it is challenged by cookie authentication first (302 to Login). Confirmed this is generic
    // behavior, not specific to this route, by probing an unrelated nonsense path
    // ("/this-route-does-not-exist-abcxyz") side by side: identical 302-to-Login response. This is
    // still the correct proof that Register no longer exists as a working page -- an unauthenticated
    // visitor gets bounced exactly like they would for any other unmapped URL, with no trace that
    // Register specifically ever existed.
    [Fact]
    public async Task Get_Register_WithoutAuthentication_RedirectsToLogin_JustLikeAnyOtherUnmappedRoute()
    {
        var client = CreateClient();

        var registerResponse = await client.GetAsync("/Account/Register");
        var nonsenseResponse = await client.GetAsync("/this-route-does-not-exist-abcxyz");

        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        registerResponse.Headers.Location!.AbsolutePath.Should().Be("/Account/Login");
        nonsenseResponse.StatusCode.Should().Be(registerResponse.StatusCode);
        nonsenseResponse.Headers.Location!.AbsolutePath.Should().Be(registerResponse.Headers.Location!.AbsolutePath);
    }

    // The complementary half: once past the authentication gate, the route genuinely doesn't exist as
    // a page either -- an authenticated user reaches the Router, which renders NotFoundPage (404, URL
    // preserved), the mechanism the ticket originally described.
    [Fact]
    public async Task Get_Register_WhenAuthenticated_ReturnsNotFound()
    {
        await CreateUserAsync("authenticated_051_user", requirePasswordChange: false);
        var client = await CreateSignedInClientAsync("authenticated_051_user");

        var response = await client.GetAsync("/Account/Register");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Non-regression: the removal of a sibling file in the same Account/Pages/ folder must not
    // affect Login's own static-SSR rendering (Lot 049).
    [Fact]
    public async Task Get_Login_StillReturnsOkWithTheLoginForm()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/Account/Login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"Input.UserName\"");
    }

    // Non-regression: same folder, same static rendering mode (Lot 049/045).
    [Fact]
    public async Task Get_ForcePasswordChange_WithFlaggedUser_StillReturnsOkWithTheForm()
    {
        await CreateUserAsync("flagged_user_051", requirePasswordChange: true);
        var client = await CreateSignedInClientAsync("flagged_user_051");

        var response = await client.GetAsync("/Account/ForcePasswordChange");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"force-password-change-form\"");
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task CreateUserAsync(string userName, bool requirePasswordChange)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName + "@example.com",
            FirstName = "Test",
            LastName = "User",
            RequirePasswordChangeOnFirstLogin = requirePasswordChange,
        };

        var result = await userManager.CreateAsync(user, "TempP@ssw0rd!");
        result.Succeeded.Should().BeTrue(
            "the test user must exist: " + string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    private async Task<HttpClient> CreateSignedInClientAsync(string userName)
    {
        var client = CreateClient();

        var loginPage = await client.GetAsync("/Account/Login");
        var fields = ReadHiddenFields(await loginPage.Content.ReadAsStringAsync());
        fields["Input.UserName"] = userName;
        fields["Input.Password"] = "TempP@ssw0rd!";
        fields["Input.RememberMe"] = "false";

        var signIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(fields));
        signIn.StatusCode.Should().Be(HttpStatusCode.Redirect, "sign-in must succeed for this test to mean anything");
        return client;
    }

    private static Dictionary<string, string> ReadHiddenFields(string html)
    {
        var fields = new Dictionary<string, string>();
        foreach (System.Text.RegularExpressions.Match input in System.Text.RegularExpressions.Regex.Matches(html, "<input[^>]*type=\"hidden\"[^>]*>"))
        {
            var name = System.Text.RegularExpressions.Regex.Match(input.Value, "name=\"([^\"]*)\"").Groups[1].Value;
            if (name.Length == 0)
            {
                continue;
            }

            fields[name] = WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(input.Value, "value=\"([^\"]*)\"").Groups[1].Value);
        }

        return fields;
    }
}
