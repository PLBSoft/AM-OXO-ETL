using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 060 (60.2): completes the M3 model with container/on-container pairs for primary/danger
// (error)/success/warning/info -- the same tonal-palette algorithm (seed = the CURRENT base color,
// standard M3 tons 90/10 light, 30/90 dark) already used for --m3-secondary-container at lot 058.
// Base colors themselves are untouched (guarded here too, redundantly with
// ThemeM3PaletteChangeScopeTests, since this file is specifically about "did adding containers
// disturb the base colors"). Reuses ThemeSecondaryButtonContrastTests' plain-text-read idiom and its
// ContrastRatio calculator convention (reimplemented here rather than shared across test files, per
// this project's no-shared-test-helper-across-files convention for Styling/ so far).
public class ThemePrimaryDangerSuccessWarningInfoContainerTests
{
    private const double MinimumContrastRatio = 4.5;

    private static string ThemeCss { get; } = File.ReadAllText(ThemeM3CssPath());

    public static IEnumerable<object[]> LightThemeContainerPairs() =>
    [
        ["primary", "#FFDAD4", "#410100"],
        ["danger", "#FFDAD5", "#410002"],
        ["success", "#A3F69C", "#002204"],
        ["warning", "#FFDBCB", "#341100"],
        ["info", "#8CF2FB", "#002022"],
    ];

    public static IEnumerable<object[]> DarkThemeContainerPairs() =>
    [
        ["primary", "#930200", "#FFDAD4"],
        ["danger", "#930009", "#FFDAD5"],
        ["success", "#005312", "#A3F69C"],
        ["warning", "#783100", "#FFDBCB"],
        ["info", "#004F54", "#8CF2FB"],
    ];

    [Theory]
    [MemberData(nameof(LightThemeContainerPairs))]
    public void LightTheme_ContainerPair_HasExpectedValuesAndMeetsContrast(string role, string expectedContainer, string expectedOnContainer)
    {
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");

        ExtractHexValue(lightBlock, $"--m3-{role}-container").Should().Be(expectedContainer);
        ExtractHexValue(lightBlock, $"--m3-on-{role}-container").Should().Be(expectedOnContainer);
        ContrastRatio(expectedOnContainer, expectedContainer).Should().BeGreaterThanOrEqualTo(MinimumContrastRatio);
    }

    [Theory]
    [MemberData(nameof(DarkThemeContainerPairs))]
    public void DarkTheme_ContainerPair_HasExpectedValuesAndMeetsContrast(string role, string expectedContainer, string expectedOnContainer)
    {
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        ExtractHexValue(darkBlock, $"--m3-{role}-container").Should().Be(expectedContainer);
        ExtractHexValue(darkBlock, $"--m3-on-{role}-container").Should().Be(expectedOnContainer);
        ContrastRatio(expectedOnContainer, expectedContainer).Should().BeGreaterThanOrEqualTo(MinimumContrastRatio);
    }

    [Fact]
    public void BaseColors_KeepTheirExactCurrentValue_InBothThemes()
    {
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        ExtractHexValue(lightBlock, "--m3-primary").Should().Be("#D81F11");
        ExtractHexValue(lightBlock, "--m3-danger").Should().Be("#BA1A1A");
        ExtractHexValue(lightBlock, "--m3-success").Should().Be("#2E7D32");
        ExtractHexValue(lightBlock, "--m3-warning").Should().Be("#ff7518");
        ExtractHexValue(lightBlock, "--m3-info").Should().Be("#008c94");

        ExtractHexValue(darkBlock, "--m3-primary").Should().Be("#FFB4AB");
        ExtractHexValue(darkBlock, "--m3-danger").Should().Be("#FFB4AB");
        ExtractHexValue(darkBlock, "--m3-success").Should().Be("#81C784");
        ExtractHexValue(darkBlock, "--m3-warning").Should().Be("#edb050");
        ExtractHexValue(darkBlock, "--m3-info").Should().Be("#13d9e3");
    }

    [Fact]
    public void NoRgbVariant_WasAddedForAnyOfTheseFiveNewContainers()
    {
        // YAGNI scope decision (60.2): no live consumer exists for these containers yet, so no
        // -rgb variant either -- unlike --m3-secondary-container-rgb, which exists only because
        // .btn-secondary genuinely consumes it (lot 058).
        foreach (var role in new[] { "primary", "danger", "success", "warning", "info" })
        {
            ThemeCss.Should().NotContain($"--m3-{role}-container-rgb");
        }
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
