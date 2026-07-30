using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 060 (60.5): written before 60.1-60.4 and meant to stay green throughout the whole lot -- a
// guard-rail that only --m3-secondary/--m3-tertiary (and their new container pairs) are the ones
// this lot is allowed to touch. primary/danger/success/warning/info base colors must keep exactly
// their pre-lot-060 values in both themes. If this test goes red mid-lot, an out-of-scope color was
// touched by mistake. Reuses ThemeSecondaryButtonContrastTests' plain-text-read idiom.
public class ThemeM3PaletteChangeScopeTests
{
    private static string ThemeCss { get; } = File.ReadAllText(ThemeM3CssPath());

    public static IEnumerable<object[]> LightThemeBaseColors() =>
    [
        ["--m3-primary", "#D81F11"],
        ["--m3-danger", "#BA1A1A"],
        ["--m3-success", "#2E7D32"],
        ["--m3-warning", "#ff7518"],
        ["--m3-info", "#008c94"],
    ];

    public static IEnumerable<object[]> DarkThemeBaseColors() =>
    [
        ["--m3-primary", "#FFB4AB"],
        ["--m3-danger", "#FFB4AB"],
        ["--m3-success", "#81C784"],
        ["--m3-warning", "#edb050"],
        ["--m3-info", "#13d9e3"],
    ];

    [Theory]
    [MemberData(nameof(LightThemeBaseColors))]
    public void LightTheme_BaseColorTokens_KeepTheirPreLot060Value(string token, string expectedHex)
    {
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");

        ExtractHexValue(lightBlock, token).Should().Be(expectedHex);
    }

    [Theory]
    [MemberData(nameof(DarkThemeBaseColors))]
    public void DarkTheme_BaseColorTokens_KeepTheirPreLot060Value(string token, string expectedHex)
    {
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        ExtractHexValue(darkBlock, token).Should().Be(expectedHex);
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
