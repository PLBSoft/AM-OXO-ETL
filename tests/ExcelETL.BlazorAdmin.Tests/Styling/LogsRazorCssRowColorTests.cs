using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 060 (60.1.d): `.log-row-error`/`.log-row-warning` had their border-left correctly on
// var(--bs-danger)/var(--bs-warning), but their tinted background used the Bootstrap *default*
// RGB triplets (220,53,69 / 255,193,7) instead of --m3-danger-rgb/--m3-warning-rgb -- so the tint
// stayed the default red/amber in dark theme instead of following the actual M3 palette values.
// Reuses ThemeSecondaryButtonContrastTests' plain-text-read idiom (no bUnit color computation).
public class LogsRazorCssRowColorTests
{
    private static string LogsCss { get; } = File.ReadAllText(LogsCssPath());

    [Fact]
    public void LogRowError_BackgroundReferencesDangerRgbToken_NotTheBootstrapDefaultTriplet()
    {
        var rule = ExtractRule(LogsCss, ".log-row-error {");

        rule.Should().Contain("rgba(var(--m3-danger-rgb), 0.08)");
        rule.Should().NotContain("220, 53, 69");
    }

    [Fact]
    public void LogRowWarning_BackgroundReferencesWarningRgbToken_NotTheBootstrapDefaultTriplet()
    {
        var rule = ExtractRule(LogsCss, ".log-row-warning {");

        rule.Should().Contain("rgba(var(--m3-warning-rgb), 0.08)");
        rule.Should().NotContain("255, 193, 7");
    }

    [Fact]
    public void LogRowErrorAndWarning_KeepTheirBorderLeftOnTheBsTokens_NonRegression()
    {
        var errorRule = ExtractRule(LogsCss, ".log-row-error {");
        var warningRule = ExtractRule(LogsCss, ".log-row-warning {");

        errorRule.Should().Contain("border-left: 4px solid var(--bs-danger);");
        warningRule.Should().Contain("border-left: 4px solid var(--bs-warning);");
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

    private static string LogsCssPath() =>
        Path.Combine(RepositoryRoot(), "src", "ExcelETL.BlazorAdmin", "Components", "Pages", "Admin", "Logs.razor.css");
}
