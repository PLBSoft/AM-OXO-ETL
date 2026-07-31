using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// wwwroot/js/theme.js is plain global script, never exercised by bUnit (no browser/JS engine
// there) -- reads it as plain text and asserts its declared surface, same "walk up to
// ExcelETL.slnx" idiom as ThemeSecondaryButtonContrastTests.cs (Lot 058). Real behavior (attribute
// actually applied before paint, localStorage persistence, system-preference fallback) remains a
// manual browser check, consistent with every other CSS/JS-only change in this project's history.
public class ThemeToggleScriptTests
{
    private static string ThemeJs { get; } = File.ReadAllText(ThemeJsPath());

    [Fact]
    public void ExposesAmOxoThemeGlobal_WithGetSetToggleApply()
    {
        ThemeJs.Should().Contain("window.amOxoTheme");
        ThemeJs.Should().Contain("get:");
        ThemeJs.Should().Contain("set:");
        ThemeJs.Should().Contain("toggle:");
        ThemeJs.Should().Contain("apply:");
    }

    [Fact]
    public void SetsDataBsThemeAttributeOnTheDocumentElement()
    {
        ThemeJs.Should().Contain("document.documentElement.setAttribute(\"data-bs-theme\"");
        ThemeJs.Should().Contain("document.documentElement.getAttribute(\"data-bs-theme\")");
    }

    [Fact]
    public void PersistsTheExplicitChoiceToLocalStorage()
    {
        ThemeJs.Should().Contain("window.localStorage.setItem(STORAGE_KEY, theme)");
    }

    [Fact]
    public void FallsBackToSystemPreference_WhenNothingStoredYet()
    {
        ThemeJs.Should().Contain("window.matchMedia");
        ThemeJs.Should().Contain("prefers-color-scheme: dark");
    }

    [Fact]
    public void AppliesTheThemeImmediatelyOnLoad_NotOnlyOnDemand()
    {
        // The apply() call at module scope (not inside a function/event handler) is what avoids a
        // light-then-dark flash -- it must run synchronously as the script parses.
        ThemeJs.TrimEnd().Should().EndWith("window.amOxoTheme.apply();");
    }

    private static string ThemeJsPath()
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

        return Path.Combine(directory.FullName, "src", "ExcelETL.BlazorAdmin", "wwwroot", "js", "theme.js");
    }
}
