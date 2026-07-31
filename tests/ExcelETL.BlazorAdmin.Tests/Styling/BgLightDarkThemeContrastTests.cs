using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Reported by the client: `.card.bg-light` (SheetRuleForm.razor/SheetGenerationRuleForm.razor/
// Users.razor's grouping sub-cards) stayed a fixed near-white Bootstrap default (`--bs-light`,
// declared once at :root in the vendored bootstrap.css, never touched by Bootstrap's own
// [data-bs-theme=dark] block) while its text kept reading --bs-card-color (--m3-on-surface, a
// *light* color meant for a dark surface) -- light text on a light background in dark theme.
// Fixed by aliasing --bs-light/--bs-light-rgb onto --m3-surface/-surface-rgb in both theme blocks,
// same tone a plain `.card` already uses (none of these `.bg-light` cards nest inside a second
// `.card`, so there's no visual-hierarchy need for a distinct shade). Reads theme-m3.css as plain
// text, same convention as ThemeSecondaryButtonContrastTests.cs (Lot 058) -- not testable in
// bUnit (no CSS cascade/computed-style resolution there).
public class BgLightDarkThemeContrastTests
{
    private const double MinimumContrastRatio = 4.5;

    private static string ThemeCss { get; } = File.ReadAllText(ThemeM3CssPath());

    [Fact]
    public void SurfaceRgbTokens_AreDeclaredInBothLightAndDarkBlocks()
    {
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        foreach (var block in new[] { lightBlock, darkBlock })
        {
            block.Should().Contain("--m3-surface-rgb:");
        }
    }

    [Fact]
    public void BsLightTokens_ReferenceTheSurfaceTokens_NotALiteralHexValue_InBothBlocks()
    {
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        foreach (var block in new[] { lightBlock, darkBlock })
        {
            block.Should().Contain("--bs-light: var(--m3-surface);");
            block.Should().Contain("--bs-light-rgb: var(--m3-surface-rgb);");
        }
    }

    [Fact]
    public void LightThemeSurfaceOnSurfacePair_MeetsWcagAaContrast()
    {
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");
        var background = ExtractHexValue(lightBlock, "--m3-surface");
        // --m3-on-surface is `var(--m3-on-background)`, not a literal hex -- resolve through it.
        var foreground = ExtractHexValue(lightBlock, "--m3-on-background");

        ContrastRatio(foreground, background).Should().BeGreaterThanOrEqualTo(MinimumContrastRatio);
    }

    [Fact]
    public void DarkThemeSurfaceOnSurfacePair_MeetsWcagAaContrast()
    {
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");
        var background = ExtractHexValue(darkBlock, "--m3-surface");
        var foreground = ExtractHexValue(darkBlock, "--m3-on-background");

        ContrastRatio(foreground, background).Should().BeGreaterThanOrEqualTo(MinimumContrastRatio);
    }

    [Fact]
    public void SurfaceRgbToken_MatchesTheSurfaceHexValue_InBothBlocks()
    {
        // Guards against the -rgb sibling silently drifting from the hex it's supposed to mirror
        // (there is no browser/CSS engine here to catch that for us).
        foreach (var themeMarker in new[] { "[data-bs-theme=\"light\"] {", "[data-bs-theme=\"dark\"] {" })
        {
            var block = ExtractBlock(ThemeCss, themeMarker);
            var hex = ExtractHexValue(block, "--m3-surface");
            var rgb = ExtractRawValue(block, "--m3-surface-rgb");

            var (r, g, b) = ParseHex(hex);
            rgb.Should().Be($"{r}, {g}, {b}");
        }
    }

    // ------------------------------------------------------------------------------------------
    // Plain-text extraction helpers -- copied from ThemeSecondaryButtonContrastTests.cs's own
    // convention rather than shared, per this project's no-shared-test-helper precedent.
    // ------------------------------------------------------------------------------------------

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

    private static string ExtractRawValue(string cssBlock, string variableName) => ExtractHexValue(cssBlock, variableName);

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
