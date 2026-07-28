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

namespace ExcelETL.BlazorAdmin.Tests.Components;

// Lot 049 (49.2): App.razor computes <Routes>'s render mode per request. These tests lock both
// halves of that behaviour over real HTTP -- Account/ pages must stay static SSR, admin pages must
// stay interactive. Getting either one wrong is a whole-page defect no bUnit test can see:
//  - an interactive Account/ page is the "Introuvable" bug this lot fixes, and would also break
//    PasswordSignInAsync (the auth cookie cannot be written once a circuit has started);
//  - a static admin page would silently drop every @onclick/@bind in the admin UI.
public class AppRenderModeHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestPassword = "TestP@ssw0rd!";

    // Marker blazor.web.js looks for to boot an interactive Server circuit over prerendered markup.
    private const string InteractiveServerComponentMarker = "\"type\":\"server\"";

    private readonly WebApplicationFactory<Program> _factory;

    public AppRenderModeHttpTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "AppRenderModeHttpTests_" + Guid.NewGuid();

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

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    public async Task AnonymousAccountPage_IsServedAsStaticSsr(string url)
    {
        var client = CreateClient();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain(InteractiveServerComponentMarker);
    }

    // Explicit non-regression on the page the whole login journey depends on: it must still render
    // its form, and it must not have been dragged into interactive rendering by 49.2.
    [Fact]
    public async Task LoginPage_StillRendersItsForm()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/Account/Login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"Input.UserName\"");
        html.Should().Contain("id=\"Input.Password\"");
    }

    [Fact]
    public async Task AdminPage_KeepsItsInteractiveServerCircuit()
    {
        await CreateAdminUserAsync("render_mode_admin");
        var client = await CreateSignedInClientAsync("render_mode_admin");

        var response = await client.GetAsync("/import-profiles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain(InteractiveServerComponentMarker);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task CreateAdminUserAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(IdentitySeeder.AdminRoleName))
        {
            await roleManager.CreateAsync(new IdentityRole(IdentitySeeder.AdminRoleName));
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName + "@example.com",
            FirstName = "Render",
            LastName = "Mode",
        };

        var created = await userManager.CreateAsync(user, TestPassword);
        created.Succeeded.Should().BeTrue(
            "the test admin must exist: " + string.Join(", ", created.Errors.Select(e => e.Description)));
        (await userManager.AddToRoleAsync(user, IdentitySeeder.AdminRoleName)).Succeeded.Should().BeTrue();
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
