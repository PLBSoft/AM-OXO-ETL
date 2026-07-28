using System.Net;
using System.Text.RegularExpressions;
using ExcelETL.Application.Identity;
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
        await CreateUserAsync("flagged_user", requirePasswordChange: true);
        var client = await CreateSignedInClientAsync("flagged_user");

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
        await CreateUserAsync("flagged_user", requirePasswordChange: true);
        var client = await CreateSignedInClientAsync("flagged_user");

        var response = await client.GetAsync(ForcePasswordChangeUrl);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain(InteractiveServerComponentMarker);
    }

    // Lot 045 (45.2) rule, never verified above the component level until now.
    [Fact]
    public async Task Get_WithUnflaggedUser_RedirectsAwayFromTheForcedChangeForm()
    {
        await CreateUserAsync("ordinary_user", requirePasswordChange: false);
        var client = await CreateSignedInClientAsync("ordinary_user");

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

    // Lot 049 (49.5): the whole reported client journey, in one test, with no manual SQL in the
    // middle. This is the test that should have existed at Lot 045 and is this lot's closing
    // condition -- every step below is a real HTTP request against a real host.
    [Fact]
    public async Task AdminCreatedUser_ChangesTemporaryPassword_AndThenUsesTheApplicationNormally()
    {
        // Lot 050 (D1): the connection identifier is now an explicit, independent username -- the
        // email itself is no longer a valid sign-in value (it contains '@', outside the allowed
        // character set), so this journey signs in with the username throughout.
        const string userName = "journey_user";
        const string email = "journey-user@example.com";
        const string newPassword = "BrandNewP@ssw0rd!";
        var temporaryPassword = await CreateUserThroughAdminFlowAsync(userName, email);
        var client = CreateClient();

        // 1. Signing in with the temporary password lands on the forced-change page, not on ReturnUrl.
        var signIn = await SignInAsync(client, userName, temporaryPassword);
        signIn.StatusCode.Should().Be(HttpStatusCode.Redirect);
        signIn.Headers.Location!.AbsolutePath.Should().Be(ForcePasswordChangeUrl);

        // 2. That page is actually reachable and shows its form (the defect this lot fixes).
        var formPage = await client.GetAsync(ForcePasswordChangeUrl);
        formPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var formHtml = await formPage.Content.ReadAsStringAsync();
        formHtml.Should().Contain("id=\"force-password-change-form\"");
        // Verified by experiment: every other step of this journey passes even with the defect in
        // place, because HttpClient never boots the SignalR circuit that replaces the body with
        // NotFoundPage in a real browser. This one assertion is what ties the journey to the root
        // cause, so undoing the render-mode fix fails here rather than shipping a green suite.
        formHtml.Should().NotContain(InteractiveServerComponentMarker);

        // 3. Submitting a valid new password lifts the flag in the database and redirects home.
        var fields = ReadHiddenFields(formHtml);
        fields["Input.CurrentPassword"] = temporaryPassword;
        fields["Input.NewPassword"] = newPassword;
        fields["Input.ConfirmNewPassword"] = newPassword;
        var change = await client.PostAsync(ForcePasswordChangeUrl, new FormUrlEncodedContent(fields));
        change.StatusCode.Should().Be(HttpStatusCode.Redirect);
        change.Headers.Location!.AbsolutePath.Should().Be("/");
        (await ReadRequirePasswordChangeFlagAsync(userName)).Should().BeFalse();

        // 4. The guard no longer fires: an ordinary page is served instead of a redirection back.
        var afterChange = await client.GetAsync("/import-profiles");
        afterChange.StatusCode.Should().Be(HttpStatusCode.OK);
        (await afterChange.Content.ReadAsStringAsync()).Should().NotContain("id=\"force-password-change-form\"");

        // 5. Logging out and back in with the *new* password goes straight to the application.
        await SignOutAsync(client, formHtml);
        var secondSignIn = await SignInAsync(client, userName, newPassword);
        secondSignIn.StatusCode.Should().Be(HttpStatusCode.Redirect);
        secondSignIn.Headers.Location!.AbsolutePath.Should().Be("/");

        var home = await client.GetAsync("/");
        home.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // Goes through the real Lot 044 admin creation path (server-generated temporary password,
    // RequirePasswordChangeOnFirstLogin set by the service itself) rather than hand-building the
    // user, so the journey starts from the exact state a real admin action produces. The Admin role
    // is added afterwards only so step 4 above has an authorized page to land on -- creating an
    // Admin is deliberately impossible from the UI (Lot 044), and that rule is untouched here.
    private async Task<string> CreateUserThroughAdminFlowAsync(string userName, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManagement = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var creation = await userManagement.CreateUserAsync(userName, email, "Journey", "User");
        creation.Succeeded.Should().BeTrue(
            "the admin-created account must exist: " + string.Join(", ", creation.Errors));

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(IdentitySeeder.AdminRoleName))
        {
            await roleManager.CreateAsync(new IdentityRole(IdentitySeeder.AdminRoleName));
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(creation.UserId!);
        (await userManager.AddToRoleAsync(user!, IdentitySeeder.AdminRoleName)).Succeeded.Should().BeTrue();

        return creation.TemporaryPassword!;
    }

    private async Task<bool> ReadRequirePasswordChangeFlagAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);
        return user!.RequirePasswordChangeOnFirstLogin;
    }

    // /Account/Logout is a plain minimal API POST (interactive components cannot write auth cookies),
    // so it needs the antiforgery token and returnUrl the NavMenu logout form renders.
    private static async Task SignOutAsync(HttpClient client, string pageHtmlCarryingTheLogoutForm)
    {
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = ReadHiddenFields(pageHtmlCarryingTheLogoutForm)["__RequestVerificationToken"],
            ["returnUrl"] = "Account/Login",
        };

        var response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(fields));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "sign-out must succeed for this test to mean anything");
    }

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
