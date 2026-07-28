using System.Net;
using System.Text.RegularExpressions;
using ExcelETL.Infrastructure.Diagnostics;
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

namespace ExcelETL.BlazorAdmin.Tests.Authorization;

// Lot 052 (52.1): the real authorization layer -- convention-autorisation-pages-blazoradmin.md's
// §2/§3. bUnit never proves a route is reachable or refused (lots 049/051's own lesson,
// reaffirmed by this lot's own note d'efficacité) -- every assertion here is a real HTTP request
// against a real host.
public class BusinessPageAuthorizationHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestPassword = "TestP@ssw0rd!";

    // 52.0 inventory: every business-function route, now Authenticated (no role) per the convention.
    // One assertion per route (a Theory case each), so a failure names the exact route at fault.
    private static readonly string[] BusinessRoutes =
    [
        "/",
        "/import-profiles",
        "/import-profiles/new",
        "/export-profiles",
        "/export-profiles/new",
        "/import-profiles/test",
        "/export-profiles/test",
        "/api-test",
        "/generated-files",
        "/profile",
    ];

    // Admin-only routes -- 52.0 confirmed /logs' page-level attribute was, before this lot, a bare
    // [Authorize] (no Roles) despite its nav link already being Admin-only -- a real one-layer-only
    // gap the convention doc's §3 calls out. Both stay Admin at the (real) page-attribute layer.
    private static readonly string[] AdminOnlyRoutes = ["/users", "/logs"];

    private readonly WebApplicationFactory<Program> _factory;

    public BusinessPageAuthorizationHttpTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "BusinessPageAuthorizationHttpTests_" + Guid.NewGuid();

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

                // /logs (SystemLogRepository) reads through its own DbContext, otherwise pointed at
                // a real, unreachable SQL Server connection string in this test environment.
                services.RemoveAll<DbContextOptions<SystemLogsDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SystemLogsDbContext>>();
                services.AddDbContextFactory<SystemLogsDbContext>(
                    options => options.UseInMemoryDatabase(databaseName + "_systemlogs"));
            });
        });
    }

    public static IEnumerable<object[]> BusinessRouteCases() => BusinessRoutes.Select(route => new object[] { route });

    public static IEnumerable<object[]> AdminOnlyRouteCases() => AdminOnlyRoutes.Select(route => new object[] { route });

    [Theory]
    [MemberData(nameof(BusinessRouteCases))]
    public async Task NonAdminAccount_CanReachEveryBusinessRoute(string route)
    {
        var client = await CreateSignedInClientAsync("non_admin_" + Guid.NewGuid().ToString("N")[..8]);

        var response = await client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"route {route} must be reachable to an authenticated account without a role");
    }

    [Theory]
    [MemberData(nameof(AdminOnlyRouteCases))]
    public async Task NonAdminAccount_IsDeniedAccessToAdminOnlyRoutes(string route)
    {
        var client = await CreateSignedInClientAsync("non_admin_" + Guid.NewGuid().ToString("N")[..8]);

        var response = await client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, $"route {route} must refuse an account without the Admin role");
        response.Headers.Location!.AbsolutePath.Should().Be("/Account/AccessDenied");
    }

    // Non-regression: this lot widens access, it never narrows it -- every existing assertion on an
    // Admin account must stay green without modification.
    [Theory]
    [MemberData(nameof(BusinessRouteCases))]
    [MemberData(nameof(AdminOnlyRouteCases))]
    public async Task AdminAccount_CanReachEveryRoute_BusinessAndAdministrative(string route)
    {
        var client = await CreateSignedInAdminClientAsync("admin_" + Guid.NewGuid().ToString("N")[..8]);

        var response = await client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"an Admin account must still reach {route}");
    }

    // Non-regression on the FallbackPolicy established at Lot 051: an unauthenticated request to a
    // business route is still bounced to Login, never to AccessDenied (that outcome is reserved for
    // an authenticated account with insufficient rights -- convention doc §6).
    [Theory]
    [MemberData(nameof(BusinessRouteCases))]
    public async Task UnauthenticatedRequest_ToABusinessRoute_RedirectsToLogin(string route)
    {
        var client = CreateClient();

        var response = await client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("Account/Login");
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<HttpClient> CreateSignedInClientAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
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

        return await SignInAsync(userName);
    }

    private async Task<HttpClient> CreateSignedInAdminClientAsync(string userName)
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
            FirstName = "Admin",
            LastName = "User",
        };
        var created = await userManager.CreateAsync(user, TestPassword);
        created.Succeeded.Should().BeTrue(
            "the test admin must exist: " + string.Join(", ", created.Errors.Select(e => e.Description)));
        (await userManager.AddToRoleAsync(user, IdentitySeeder.AdminRoleName)).Succeeded.Should().BeTrue();

        return await SignInAsync(userName);
    }

    private async Task<HttpClient> SignInAsync(string userName)
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
