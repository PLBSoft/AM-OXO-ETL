using System.Net;
using System.Text.RegularExpressions;
using ExcelETL.Application.Identity;
using ExcelETL.Infrastructure.Identity;
using ExcelETL.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Account;

// Lot 050 (50.10). Lesson from Lot 049: no bUnit test proves a sign-in identity genuinely works --
// PasswordSignInAsync, the real UserManager pipeline and the real host's IdentityOptions are all
// bypassed by rendering a component directly. This lot changes what users sign in with, so it needs
// at least one real HTTP journey: a distinct username signs in, the email does not.
public class UserNameLoginIdentityHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UserNameLoginIdentityHttpTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "UserNameLoginIdentityHttpTests_" + Guid.NewGuid();

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
    public async Task AdminCreatedUser_SignsInWithUserName_ButNotWithEmail()
    {
        const string userName = "TST_01";
        const string email = "test@exemple.fr";
        string temporaryPassword;

        using (var scope = _factory.Services.CreateScope())
        {
            var userManagement = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
            var creation = await userManagement.CreateUserAsync(userName, email, "Test", "User");
            creation.Succeeded.Should().BeTrue(
                "the test account must exist: " + string.Join(", ", creation.Errors));
            temporaryPassword = creation.TemporaryPassword!;
        }

        var client = CreateClient();

        var signInWithUserName = await SignInAsync(client, userName, temporaryPassword);
        signInWithUserName.StatusCode.Should().Be(HttpStatusCode.Redirect);
        signInWithUserName.Headers.Location!.AbsolutePath.Should().Be("/Account/ForcePasswordChange");

        var signInWithEmail = await SignInAsync(CreateClient(), email, temporaryPassword);
        signInWithEmail.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await signInWithEmail.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"Input.UserName\"", "a failed sign-in re-renders the login form, not a redirect");
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string userName, string password)
    {
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
