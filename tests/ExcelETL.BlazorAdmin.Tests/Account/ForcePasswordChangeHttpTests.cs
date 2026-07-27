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

// Lot 049 (49.1/49.5): the tests that were missing at Lot 045. Its bUnit tests render
// ForcePasswordChange.razor directly, which short-circuits routing, the render mode and the whole
// HTTP pipeline -- precisely the layers the "Introuvable" defect lived in. Everything here goes
// through a real HTTP request against a real host.
public class ForcePasswordChangeHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ForcePasswordChangeUrl = "/Account/ForcePasswordChange";
    private const string TemporaryPassword = "TempP@ssw0rd!";

    // Marker blazor.web.js looks for to boot an interactive Server circuit over a prerendered
    // component. Its presence on an [ExcludeFromInteractiveRouting] page is the defect itself: the
    // interactive route table excludes those pages, so the circuit replaces the correctly-rendered
    // body with NotFoundPage (see the 49.0 diagnostic in this lot's ticket document).
    private const string InteractiveServerComponentMarker = "\"type\":\"server\"";

    private readonly WebApplicationFactory<Program> _factory;

    public ForcePasswordChangeHttpTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "ForcePasswordChangeHttpTests_" + Guid.NewGuid();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            // Development is what the reported defect was reproduced under (Visual Studio), and it
            // keeps UseHsts/UseExceptionHandler out of the way so a server-side failure surfaces as
            // itself rather than as a rewritten error page.
            builder.UseEnvironment("Development");
            builder.UseSetting("Serilog:EnableMsSqlServerSink", "false");
            // Same switches, and the same reasons, as the WebAPI integration tests: neither database
            // is a real SQL Server here, and concurrent seeding races on shared rows.
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("IdentitySeeding:Enabled", "false");
            builder.UseSetting("ProfileSeeding:Enabled", "false");
            // Program.cs fails fast when this section is missing (no production default is
            // committed), so the host cannot start without it -- the values are never called.
            builder.UseSetting("OxoApiTestClient:BaseUrl", "https://localhost:7088/");
            builder.UseSetting("OxoApiTestClient:ApiKey", "unused-in-tests");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ExcelEtlDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ExcelEtlDbContext>>();
                services.AddDbContextFactory<ExcelEtlDbContext>(
                    options => options.UseInMemoryDatabase(databaseName));

                // ApplicationIdentityDbContext is registered twice by Program.cs (AddDbContext for
                // AddEntityFrameworkStores, AddDbContextFactory for IUserRepository) -- both have to
                // be swapped, onto the same in-memory database so they see the same users.
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
    public async Task Get_WithFlaggedUser_ReturnsOkAndTheForcedChangeForm()
    {
        await CreateUserAsync("flagged-user", requirePasswordChange: true);
        var client = await CreateSignedInClientAsync("flagged-user");

        var response = await client.GetAsync(ForcePasswordChangeUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"force-password-change-form\"");
        html.Should().Contain("id=\"current-password-input\"");
        html.Should().Contain("id=\"new-password-input\"");
        html.Should().Contain("id=\"confirm-password-input\"");
        html.Should().Contain("id=\"force-password-change-submit\"");
    }

    // The assertion that actually catches the reported defect. The response above is already
    // correct; what breaks the page is the interactive circuit that boots over it and re-routes.
    [Fact]
    public async Task Get_WithFlaggedUser_IsServedAsStaticSsr_WithoutAnInteractiveCircuit()
    {
        await CreateUserAsync("flagged-user", requirePasswordChange: true);
        var client = await CreateSignedInClientAsync("flagged-user");

        var response = await client.GetAsync(ForcePasswordChangeUrl);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain(InteractiveServerComponentMarker);
    }

    // Lot 045 (45.2) rule, never verified above the component level until now.
    [Fact]
    public async Task Get_WithUnflaggedUser_RedirectsAwayFromTheForcedChangeForm()
    {
        await CreateUserAsync("ordinary-user", requirePasswordChange: false);
        var client = await CreateSignedInClientAsync("ordinary-user");

        var response = await client.GetAsync(ForcePasswordChangeUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.Should().Be("/");
    }

    [Fact]
    public async Task Get_WithoutAuthentication_RedirectsToLogin()
    {
        var client = CreateClient();

        var response = await client.GetAsync(ForcePasswordChangeUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("Account/Login");
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

        var result = await userManager.CreateAsync(user, TemporaryPassword);
        result.Succeeded.Should().BeTrue(
            "the test user must exist: " + string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    private async Task<HttpClient> CreateSignedInClientAsync(string userName)
    {
        var client = CreateClient();
        var response = await SignInAsync(client, userName, TemporaryPassword);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "sign-in must succeed for this test to mean anything");
        return client;
    }

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string userName, string password)
    {
        // The login form is a Blazor SSR form: it carries both the antiforgery token and the
        // _handler field identifying its FormName, so both have to be replayed verbatim.
        var loginPage = await client.GetAsync("/Account/Login");
        var fields = ReadHiddenFields(await loginPage.Content.ReadAsStringAsync());
        fields["Input.UserName"] = userName;
        fields["Input.Password"] = password;
        fields["Input.RememberMe"] = "false";

        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(fields));
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
