using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 060 (60.1.c): `.blazor-error-boundary`/`.darker-border-checkbox` were scaffolded defaults
// hardcoding #b32121/#929292, outside the --m3-* token system and not tracking dark theme. Reuses
// ThemeSecondaryButtonContrastTests' plain-text-read idiom (no bUnit color/layout computation).
public class AppCssScaffoldedColorTests
{
    private static string AppCss { get; } = File.ReadAllText(AppCssPath());

    [Fact]
    public void BlazorErrorBoundary_ReferencesDangerToken_NotALiteralHex()
    {
        var rule = ExtractRule(AppCss, ".blazor-error-boundary {");

        rule.Should().Contain("var(--m3-danger)");
        rule.Should().NotContain("#b32121");
    }

    [Fact]
    public void DarkerBorderCheckbox_ReferencesBorderColorToken_NotALiteralHex()
    {
        var rule = ExtractRule(AppCss, ".darker-border-checkbox.form-check-input {");

        rule.Should().Contain("var(--bs-border-color)");
        rule.Should().NotContain("#929292");
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

    private static string AppCssPath() =>
        Path.Combine(RepositoryRoot(), "src", "ExcelETL.BlazorAdmin", "wwwroot", "app.css");
}
