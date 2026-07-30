using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 060 (60.1.a): `.top-row` used to hardcode #f7f7f7/#d6d5d5, staying gray in dark theme instead
// of tracking it. Fixed to var(--bs-body-bg)/var(--bs-border-color) -- a generic top banner blends
// into the page background rather than becoming a new, undeclared surface role. Reuses
// ThemeSecondaryButtonContrastTests' plain-text-read idiom (no bUnit color/layout computation).
public class MainLayoutRazorCssTopRowColorTests
{
    private static string MainLayoutCss { get; } = File.ReadAllText(MainLayoutCssPath());

    [Fact]
    public void TopRow_ReferencesBodyBackgroundAndBorderColorTokens()
    {
        var rule = ExtractRule(MainLayoutCss, ".top-row {");

        rule.Should().Contain("var(--bs-body-bg)");
        rule.Should().Contain("var(--bs-border-color)");
    }

    [Fact]
    public void TopRow_NoLongerContainsALiteralHexColor()
    {
        var rule = ExtractRule(MainLayoutCss, ".top-row {");

        rule.Should().NotMatchRegex(@"#[0-9A-Fa-f]{3,8}");
    }

    private static string ExtractRule(string css, string selectorMarker)
    {
        var startIndex = css.IndexOf(selectorMarker, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"the rule '{selectorMarker}' should exist");

        var closeBraceIndex = css.IndexOf('}', startIndex);
        return css[startIndex..(closeBraceIndex + 1)];
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

    private static string MainLayoutCssPath() =>
        Path.Combine(RepositoryRoot(), "src", "ExcelETL.BlazorAdmin", "Components", "Layout", "MainLayout.razor.css");
}
