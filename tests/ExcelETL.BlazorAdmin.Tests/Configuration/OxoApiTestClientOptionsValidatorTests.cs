using ExcelETL.BlazorAdmin.Configuration;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Configuration;

// Lot 038 (38.1): fail-fast validation, unit-tested directly since BlazorAdmin has no
// WebApplicationFactory-based integration test project to boot Program.cs's real startup path
// against (unlike ExcelETL.WebAPI's own ApiKeyAuthentication:ApiKey check).
public class OxoApiTestClientOptionsValidatorTests
{
    [Theory]
    [InlineData(null, "some-key")]
    [InlineData("", "some-key")]
    [InlineData("   ", "some-key")]
    public void ValidateOrThrow_WithMissingBaseUrl_ThrowsExplicitInvalidOperationException(string? baseUrl, string apiKey)
    {
        var act = () => OxoApiTestClientOptionsValidator.ValidateOrThrow(baseUrl, apiKey);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OxoApiTestClient:BaseUrl*");
    }

    [Theory]
    [InlineData("https://example.com", null)]
    [InlineData("https://example.com", "")]
    [InlineData("https://example.com", "   ")]
    public void ValidateOrThrow_WithMissingApiKey_ThrowsExplicitInvalidOperationException(string baseUrl, string? apiKey)
    {
        var act = () => OxoApiTestClientOptionsValidator.ValidateOrThrow(baseUrl, apiKey);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OxoApiTestClient:ApiKey*");
    }

    [Fact]
    public void ValidateOrThrow_WithBothValuesPresent_DoesNotThrow()
    {
        var act = () => OxoApiTestClientOptionsValidator.ValidateOrThrow("https://example.com", "some-key");

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateOrThrow_DoesNotSilentlyTrimOrTransformValues()
    {
        // A value that is present but padded with a stray space is not this validator's concern
        // (no silent trim that would mask a configuration typo) -- it only checks for
        // null/empty/whitespace-only, per the ticket's explicit "no implicit transformation" note.
        var act = () => OxoApiTestClientOptionsValidator.ValidateOrThrow(" https://example.com ", " some-key ");

        act.Should().NotThrow();
    }
}
