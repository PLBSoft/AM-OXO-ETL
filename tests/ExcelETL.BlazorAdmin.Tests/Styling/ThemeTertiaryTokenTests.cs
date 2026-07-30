using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 060 (60.4): adds a brand-new M3 tertiary role, absent from the file until now, coordinated
// with the Expressive secondary added at 60.3 (same seed/variant, not picked independently).
// Reuses ThemeSecondaryButtonContrastTests' plain-text-read idiom and ContrastRatio convention.
public class ThemeTertiaryTokenTests
{
    private const double MinimumContrastRatio = 4.5;

    private static string ThemeCss { get; } = File.ReadAllText(ThemeM3CssPath());

    [Fact]
    public void LightTheme_TertiaryTokens_HoldTheExpectedExpressiveValues()
    {
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");

        ExtractHexValue(lightBlock, "--m3-tertiary").Should().Be("#00687F");
        ExtractRgbValue(lightBlock, "--m3-tertiary-rgb").Should().Be("0, 104, 127");
        ExtractHexValue(lightBlock, "--m3-on-tertiary").Should().Be("#F1FAFF");
        ExtractHexValue(lightBlock, "--m3-tertiary-container").Should().Be("#5BD5FA");
        ExtractHexValue(lightBlock, "--m3-on-tertiary-container").Should().Be("#004657");
    }

    [Fact]
    public void DarkTheme_TertiaryTokens_HoldTheExpectedExpressiveValues()
    {
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        ExtractHexValue(darkBlock, "--m3-tertiary").Should().Be("#88E0FF");
        ExtractRgbValue(darkBlock, "--m3-tertiary-rgb").Should().Be("136, 224, 255");
        ExtractHexValue(darkBlock, "--m3-on-tertiary").Should().Be("#005063");
        ExtractHexValue(darkBlock, "--m3-tertiary-container").Should().Be("#5BD5FA");
        ExtractHexValue(darkBlock, "--m3-on-tertiary-container").Should().Be("#004657");
    }

    [Fact]
    public void TertiaryContainerPair_IsDeliberatelyIdenticalInLightAndDark_NotACopyPasteMistake()
    {
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        ExtractHexValue(lightBlock, "--m3-tertiary-container")
            .Should().Be(ExtractHexValue(darkBlock, "--m3-tertiary-container"));
        ExtractHexValue(lightBlock, "--m3-on-tertiary-container")
            .Should().Be(ExtractHexValue(darkBlock, "--m3-on-tertiary-container"));
    }

    [Theory]
    [InlineData("[data-bs-theme=\"light\"] {")]
    [InlineData("[data-bs-theme=\"dark\"] {")]
    public void TertiaryPairs_MeetWcagAaContrast(string blockMarker)
    {
        var block = ExtractBlock(ThemeCss, blockMarker);

        ContrastRatio(ExtractHexValue(block, "--m3-on-tertiary"), ExtractHexValue(block, "--m3-tertiary"))
            .Should().BeGreaterThanOrEqualTo(MinimumContrastRatio);
        ContrastRatio(ExtractHexValue(block, "--m3-on-tertiary-container"), ExtractHexValue(block, "--m3-tertiary-container"))
            .Should().BeGreaterThanOrEqualTo(MinimumContrastRatio);
    }

    [Fact]
    public void BootstrapNativeTertiaryTokens_AreNeverRemappedOntoM3Tertiary()
    {
        // Guard against the 60.0.7 pitfall: --bs-tertiary-bg/--bs-tertiary-color are Bootstrap's own
        // neutral-gray utility tokens (zebra table rows, disabled elements), unrelated to the M3
        // accent role added here. Merging them would silently break that existing utility.
        ThemeCss.Should().NotContain("--bs-tertiary-bg: var(--m3-tertiary");
        ThemeCss.Should().NotContain("--bs-tertiary-color: var(--m3-tertiary");
        ThemeCss.Should().NotContain("--bs-tertiary-bg:");
        ThemeCss.Should().NotContain("--bs-tertiary-color:");
    }

    [Fact]
    public void NoTertiaryContainerRgbVariant_WasAdded()
    {
        // YAGNI scope decision (60.4): no live consumer exists for tertiary-container yet, unlike
        // --m3-tertiary-rgb itself, which follows the file's existing "every accent role always
        // carries a -rgb variant" pattern (primary/secondary/tertiary), container or not.
        ThemeCss.Should().NotContain("--m3-tertiary-container-rgb");
    }

    private static string ExtractBlock(string css, string blockStartMarker)
    {
        var startIndex = css.IndexOf(blockStartMarker, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"the block starting with '{blockStartMarker}' should exist in theme-m3.css");

        var openBraceIndex = css.IndexOf('{', startIndex);
        var closeBraceIndex = css.IndexOf('}', openBraceIndex);

        return css[openBraceIndex..(closeBraceIndex + 1)];
    }

    private static string ExtractHexValue(string cssBlock, string variableName)
    {
        var marker = $"{variableName}:";
        var startIndex = cssBlock.IndexOf(marker, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"'{variableName}' should be declared in this block");

        var valueStart = startIndex + marker.Length;
        var valueEnd = cssBlock.IndexOf(';', valueStart);
        return cssBlock[valueStart..valueEnd].Trim();
    }

    private static string ExtractRgbValue(string cssBlock, string variableName) =>
        ExtractHexValue(cssBlock, variableName);

    private static double ContrastRatio(string hexA, string hexB)
    {
        var luminanceA = RelativeLuminance(hexA);
        var luminanceB = RelativeLuminance(hexB);

        var lighter = Math.Max(luminanceA, luminanceB);
        var darker = Math.Min(luminanceA, luminanceB);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var (r, g, b) = ParseHex(hex);

        var rLin = LinearizeChannel(r);
        var gLin = LinearizeChannel(g);
        var bLin = LinearizeChannel(b);

        return (0.2126 * rLin) + (0.7152 * gLin) + (0.0722 * bLin);
    }

    private static double LinearizeChannel(int channel8Bit)
    {
        var c = channel8Bit / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static (int R, int G, int B) ParseHex(string hex)
    {
        var value = hex.TrimStart('#');
        var r = Convert.ToInt32(value[..2], 16);
        var g = Convert.ToInt32(value[2..4], 16);
        var b = Convert.ToInt32(value[4..6], 16);
        return (r, g, b);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExcelETL.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the repository root (ExcelETL.slnx).");
        }

        return directory.FullName;
    }

    private static string ThemeM3CssPath() =>
        Path.Combine(RepositoryRoot(), "src", "ExcelETL.BlazorAdmin", "wwwroot", "css", "theme-m3.css");
}
