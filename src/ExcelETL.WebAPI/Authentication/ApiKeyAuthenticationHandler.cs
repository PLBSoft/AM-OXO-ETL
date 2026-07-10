using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ExcelETL.WebAPI.Authentication;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var providedKeyHeader))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing API key header."));
        }

        var providedKey = providedKeyHeader.ToString();

        if (string.IsNullOrEmpty(Options.ApiKey) || !IsValidKey(providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "LegacyApplication")], Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private bool IsValidKey(string providedKey) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedKey),
            Encoding.UTF8.GetBytes(Options.ApiKey));
}
