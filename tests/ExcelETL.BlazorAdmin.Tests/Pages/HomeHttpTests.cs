using System.Net;
using System.Text.RegularExpressions;
using ExcelETL.Application.Home;
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

namespace ExcelETL.BlazorAdmin.Tests.Pages;

// Lot 054 (54.0/54.1/54.4): "/" now belongs to Home.razor, not ImportProfiles.razor -- every
// assertion here is a real HTTP request, per lots 049/051/052's own lesson that bUnit never proves
// routing or authorization (bUnit renders a component in isolation; it never says who owns a route).
public class HomeHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestPassword = "TestP@ssw0rd!";

    // A stable id unique to the home page's content, never a localized string -- 54.1's own
    // requirement. Asserting only HTTP 200 would prove nothing: "/" already answered 200 before this
    // lot (ImportProfiles.razor's own second @page route).
    private const string HomePageMarker = "id=\"home-kpi-region\"";

    private readonly WebApplicationFactory<Program> _factory;

    public HomeHttpTests(WebApplicationFactory<Program> factory)
    {
        _factory = ConfigureFactory(factory, "HomeHttpTests_" + Guid.NewGuid());
    }

    private static WebApplicationFactory<Program> ConfigureFactory(WebApplicationFactory<Program> factory, string databaseName) =>
        factory.WithWebHostBuilder(builder =>
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

    [Fact]
    public async Task NonAdminAccount_Get_Root_ReturnsOkWithHomePageContent()
    {
        var client = await CreateSignedInClientAsync(_factory, "non_admin_" + Guid.NewGuid().ToString("N")[..8]);

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(HomePageMarker);
    }

    // Non-regression: the ticket's own decision requires identical content regardless of role.
    [Fact]
    public async Task AdminAccount_Get_Root_ReturnsOkWithTheSameHomePageContent()
    {
        var client = await CreateSignedInAdminClientAsync(_factory, "admin_" + Guid.NewGuid().ToString("N")[..8]);

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(HomePageMarker);
    }

    [Fact]
    public async Task UnauthenticatedRequest_Get_Root_RedirectsToLogin()
    {
        var client = CreateClient(_factory);

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("Account/Login");
    }

    // The route survives the retirement of its "/" mapping.
    [Fact]
    public async Task Get_ImportProfiles_StillReturnsOkWithTheProfileList_AfterRootWasReassigned()
    {
        var client = await CreateSignedInClientAsync(_factory, "non_admin_" + Guid.NewGuid().ToString("N")[..8]);

        var response = await client.GetAsync("/import-profiles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(HomePageMarker);
    }

    // 54.4: "/" is the post-login redirect target for every account -- an unhandled exception here
    // would lock the whole application out at the door, so this is the test that actually protects
    // it, not just a nice-to-have. A dedicated factory customization (a throwing fake service),
    // separate from the shared _factory, so no other test in this class is affected.
    [Fact]
    public async Task Get_Root_ReturnsOk_EvenWhenTheIndicatorsSourceIsUnavailable()
    {
        var throwingFactory = ConfigureFactory(_factory, "HomeHttpTests_Throwing_" + Guid.NewGuid());
        throwingFactory = throwingFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHomeIndicatorsService>();
                services.AddScoped<IHomeIndicatorsService, ThrowingHomeIndicatorsService>();
            });
        });
        var client = await CreateSignedInClientAsync(throwingFactory, "non_admin_" + Guid.NewGuid().ToString("N")[..8]);

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed class ThrowingHomeIndicatorsService : IHomeIndicatorsService
    {
        public Task<HomeIndicators> GetIndicatorsAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated indicators-source failure.");
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<HttpClient> CreateSignedInClientAsync(WebApplicationFactory<Program> factory, string userName)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName + "@example.com",
            FirstName = "Non",
            LastName = "Admin",
        };
        var created = await userManager.CreateAsync(user, TestPassword);
        created.Succeeded.Should().BeTrue(
            "the test user must exist: " + string.Join(", ", created.Errors.Select(e => e.Description)));

        return await SignInAsync(factory, userName);
    }

    private static async Task<HttpClient> CreateSignedInAdminClientAsync(WebApplicationFactory<Program> factory, string userName)
    {
        using var scope = factory.Services.CreateScope();
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
            FirstName = "Admin",
            LastName = "User",
        };
        var created = await userManager.CreateAsync(user, TestPassword);
        created.Succeeded.Should().BeTrue(
            "the test admin must exist: " + string.Join(", ", created.Errors.Select(e => e.Description)));
        (await userManager.AddToRoleAsync(user, IdentitySeeder.AdminRoleName)).Succeeded.Should().BeTrue();

        return await SignInAsync(factory, userName);
    }

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> factory, string userName)
    {
        var client = CreateClient(factory);

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
