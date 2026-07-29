using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 058 (58.1): reads theme-m3.css as a plain text file and asserts on its declared token
// values and consumers -- not testable in bUnit at all (no color/layout computation there). The
// contrast-ratio calculator below is a permanent test-only guard-rail against a future palette
// change silently breaking accessibility, per the ticket's own explicit rationale; it deliberately
// does not live in production code, since it's a test property, not an application feature.
// Reuses the same "walk up to ExcelETL.slnx, then descend to the BlazorAdmin project" idiom as
// ConnectionStringConfigurationTests.cs (Lot 029) -- no new mechanism needed.
public class ThemeSecondaryButtonContrastTests
{
    private const double MinimumContrastRatio = 4.5;

    private static string ThemeCss { get; } = File.ReadAllText(ThemeM3CssPath());

    [Fact]
    public void SecondaryContainerTokens_AreDeclaredInBothLightAndDarkBlocks()
    {
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        foreach (var block in new[] { lightBlock, darkBlock })
        {
            block.Should().Contain("--m3-secondary-container:");
            block.Should().Contain("--m3-on-secondary-container:");
            block.Should().Contain("--m3-secondary-container-rgb:");
        }
    }

    [Fact]
    public void BtnSecondary_ReferencesTheContainerTokens_NotALiteralHexValue()
    {
        var btnSecondaryBlock = ExtractRule(ThemeCss, ".btn-secondary {");

        btnSecondaryBlock.Should().Contain("var(--m3-secondary-container)");
        btnSecondaryBlock.Should().Contain("var(--m3-on-secondary-container)");
        btnSecondaryBlock.Should().Contain("var(--m3-secondary-container-rgb)");
        // Guard against a literal hex value sneaking into this rule instead of a var() reference.
        btnSecondaryBlock.Should().NotMatchRegex(@"#[0-9A-Fa-f]{3,8}");
    }

    [Fact]
    public void BtnOutlineSecondaryAndTextBgSecondary_StillReferenceThePlainSecondaryTokens()
    {
        var outlineBlock = ExtractRule(ThemeCss, ".btn-outline-secondary {");
        outlineBlock.Should().Contain("var(--m3-secondary)");
        outlineBlock.Should().Contain("var(--m3-on-secondary)");
        outlineBlock.Should().NotContain("--m3-secondary-container");

        ThemeCss.Should().Contain(".text-bg-secondary { color: var(--m3-on-secondary) !important; }");
    }

    [Fact]
    public void LightThemeSecondaryContainerPair_MeetsWcagAaContrast()
    {
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");
        var background = ExtractHexValue(lightBlock, "--m3-secondary-container");
        var foreground = ExtractHexValue(lightBlock, "--m3-on-secondary-container");

        ContrastRatio(foreground, background).Should().BeGreaterThanOrEqualTo(MinimumContrastRatio);
    }

    [Fact]
    public void DarkThemeSecondaryContainerPair_MeetsWcagAaContrast()
    {
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");
        var background = ExtractHexValue(darkBlock, "--m3-secondary-container");
        var foreground = ExtractHexValue(darkBlock, "--m3-on-secondary-container");

        ContrastRatio(foreground, background).Should().BeGreaterThanOrEqualTo(MinimumContrastRatio);
    }

    // ------------------------------------------------------------------------------------------
    // Plain-text extraction helpers -- no CSS parser dependency, matching the file's own small,
    // hand-authored structure.
    // ------------------------------------------------------------------------------------------

    private static string ExtractBlock(string css, string blockStartMarker)
    {
        var startIndex = css.IndexOf(blockStartMarker, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"the block starting with '{blockStartMarker}' should exist in theme-m3.css");

        var openBraceIndex = css.IndexOf('{', startIndex);
        var closeBraceIndex = css.IndexOf('}', openBraceIndex);

        return css[openBraceIndex..(closeBraceIndex + 1)];
    }

    private static string ExtractRule(string css, string selectorMarker)
    {
        var startIndex = css.IndexOf(selectorMarker, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"the rule '{selectorMarker}' should exist in theme-m3.css");

        var closeBraceIndex = css.IndexOf('}', startIndex);
        return css[startIndex..(closeBraceIndex + 1)];
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

    // WCAG 2.1 contrast ratio: (L1 + 0.05) / (L2 + 0.05), L1 the lighter of the two relative
    // luminances. A permanent test-only utility -- not a production feature.
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

    private static string ThemeM3CssPath()
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

        return Path.Combine(directory.FullName, "src", "ExcelETL.BlazorAdmin", "wwwroot", "css", "theme-m3.css");
    }
}
