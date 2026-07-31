using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// NavMenu.razor.css's icon show/hide rules react to the [data-bs-theme] attribute set on <html>
// by wwwroot/js/theme.js -- not testable in bUnit (no CSS attribute-selector cascade there). Reads
// the file as plain text, same convention as ThemeSecondaryButtonContrastTests.cs (Lot 058).
public class NavMenuThemeToggleCssTests
{
    private static string NavMenuCss { get; } = File.ReadAllText(NavMenuCssPath());

    [Fact]
    public void SunIcon_IsHiddenInDarkTheme()
    {
        NavMenuCss.Should().Contain(":root[data-bs-theme=\"dark\"] .theme-toggle-icon-sun");
    }

    [Fact]
    public void MoonIcon_IsShownOnlyInDarkTheme()
    {
        NavMenuCss.Should().MatchRegex(@"\.theme-toggle-icon-moon\s*\{\s*display:\s*none;\s*\}");
        NavMenuCss.Should().Contain(":root[data-bs-theme=\"dark\"] .theme-toggle-icon-moon");
    }

    [Fact]
    public void SelectorTargetsTheRealDocumentRoot_NotAComponentScopedClass()
    {
        // A selector scoped entirely to this component (e.g. `.sidebar-theme-toggle[data-bs-theme=...]`)
        // would never match, since [data-bs-theme] lives on <html>, outside this component's render
        // output -- the rule must read `:root[data-bs-theme=...]` so only the trailing compound
        // selector picks up Blazor's CSS-isolation scope attribute.
        NavMenuCss.Should().MatchRegex(@":root\[data-bs-theme=""dark""\]\s+\.theme-toggle-icon-(sun|moon)");
    }

    private static string NavMenuCssPath()
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

        return Path.Combine(directory.FullName, "src", "ExcelETL.BlazorAdmin", "Components", "Layout", "NavMenu.razor.css");
    }
}
