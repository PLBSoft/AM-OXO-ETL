using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 059 (59.5): `.sheet-rule-sublist-details > summary`'s color used to be var(--bs-link-color),
// which theme-m3.css maps onto --m3-primary -- the same red used for alerts/the CTA. That made a
// purely-informational disclosure summary read as an error message. Fixed to a neutral
// var(--bs-secondary-color) instead -- one declaration, one file, no theme-m3.css change at all.
// Reuses ThemeSecondaryButtonContrastTests' plain-text-read idiom (no CSS calculation happens in
// bUnit) rather than inventing a new one.
public class AppCssSheetRuleSublistDetailsColorTests
{
    private static string AppCss { get; } = File.ReadAllText(AppCssPath());
    private static string ThemeM3Css { get; } = File.ReadAllText(ThemeM3CssPath());

    [Fact]
    public void SheetRuleSublistDetailsSummary_ReferencesSecondaryColorToken()
    {
        var rule = ExtractRule(AppCss, ".sheet-rule-sublist-details > summary {");

        rule.Should().Contain("var(--bs-secondary-color)");
    }

    [Fact]
    public void SheetRuleSublistDetailsSummary_NoLongerReferencesLinkColorOrPrimaryOrALiteralHex()
    {
        var rule = ExtractRule(AppCss, ".sheet-rule-sublist-details > summary {");

        rule.Should().NotContain("--bs-link-color");
        rule.Should().NotContain("--m3-primary");
        rule.Should().NotMatchRegex(@"#[0-9A-Fa-f]{3,8}");
    }

    [Fact]
    public void SheetRuleSublistDetailsSummary_KeepsItsNonColorAffordanceRules()
    {
        var rule = ExtractRule(AppCss, ".sheet-rule-sublist-details > summary {");

        rule.Should().Contain("cursor: pointer");
        rule.Should().Contain("list-style: none");
    }

    // Guard against the fix having been applied to theme-m3.css instead of app.css: --bs-link-color
    // must still map onto --m3-primary in both theme blocks, unchanged, so real application links
    // are unaffected.
    [Fact]
    public void ThemeM3Css_StillMapsLinkColorOntoPrimary_InBothLightAndDarkBlocks()
    {
        var lightBlock = ExtractBlock(ThemeM3Css, "[data-bs-theme=\"light\"] {");
        var darkBlock = ExtractBlock(ThemeM3Css, "[data-bs-theme=\"dark\"] {");

        foreach (var block in new[] { lightBlock, darkBlock })
        {
            block.Should().Contain("--bs-link-color: var(--m3-primary)");
        }
    }

    private static string ExtractRule(string css, string selectorMarker)
    {
        var startIndex = css.IndexOf(selectorMarker, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"the rule '{selectorMarker}' should exist");

        var closeBraceIndex = css.IndexOf('}', startIndex);
        return css[startIndex..(closeBraceIndex + 1)];
    }

    private static string ExtractBlock(string css, string blockStartMarker)
    {
        var startIndex = css.IndexOf(blockStartMarker, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"the block starting with '{blockStartMarker}' should exist in theme-m3.css");

        var openBraceIndex = css.IndexOf('{', startIndex);
        var closeBraceIndex = css.IndexOf('}', openBraceIndex);

        return css[openBraceIndex..(closeBraceIndex + 1)];
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

    private static string AppCssPath() =>
        Path.Combine(RepositoryRoot(), "src", "ExcelETL.BlazorAdmin", "wwwroot", "app.css");

    private static string ThemeM3CssPath() =>
        Path.Combine(RepositoryRoot(), "src", "ExcelETL.BlazorAdmin", "wwwroot", "css", "theme-m3.css");
}
