using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 060 (60.3): replaces the taupe/brown secondary (the algorithmically-correct but
// client-disliked TonalSpot-variant default) with the M3 Expressive variant of the same seed
// (--m3-primary), which shifts hue instead of desaturating. Reuses
// ThemeSecondaryButtonContrastTests' plain-text-read idiom; the 4 pre-existing contrast tests in
// that file are dynamic (they read the file's current value, no hex hardcoded) and are relied on,
// not duplicated here, to prove non-regression of contrast after this change.
public class ThemeSecondaryHueReplacementTests
{
    private static string ThemeCss { get; } = File.ReadAllText(ThemeM3CssPath());

    [Fact]
    public void LightTheme_SecondaryTokens_HoldTheNewExpressiveValues()
    {
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");

        ExtractHexValue(lightBlock, "--m3-secondary").Should().Be("#3E6474");
        ExtractHexValue(lightBlock, "--m3-on-secondary").Should().Be("#F2FAFF");
        ExtractHexValue(lightBlock, "--m3-secondary-container").Should().Be("#C4EBFF");
        ExtractHexValue(lightBlock, "--m3-on-secondary-container").Should().Be("#325868");
    }

    [Fact]
    public void DarkTheme_SecondaryTokens_HoldTheNewExpressiveValues()
    {
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        ExtractHexValue(darkBlock, "--m3-secondary").Should().Be("#B4CAD5");
        ExtractHexValue(darkBlock, "--m3-on-secondary").Should().Be("#2E434C");
        ExtractHexValue(darkBlock, "--m3-secondary-container").Should().Be("#132831");
        ExtractHexValue(darkBlock, "--m3-on-secondary-container").Should().Be("#91A7B2");
    }

    [Fact]
    public void OldTaupeSecondaryValues_NoLongerAppearAnywhereInTheFile()
    {
        ThemeCss.Should().NotContain("#775652");
        ThemeCss.Should().NotContain("#E7BDB8");
        ThemeCss.Should().NotContain("#FFDAD6");
        ThemeCss.Should().NotContain("#2C1512");
        ThemeCss.Should().NotContain("#5D3F3B");
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
