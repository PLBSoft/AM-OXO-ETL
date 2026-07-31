using System.Linq;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Client-reported (2026-07-31): the sidebar filled with --m3-primary (a *light* salmon in dark
// theme) made it the brightest, most attention-grabbing surface on the page -- the opposite of
// dark mode's own elevation model (differentiate surfaces via low luminance, not a saturated/bright
// fill). Fixed by introducing --m3-sidebar-bg-start/-end/-fg/-active-bg/-active-fg/-hover-bg
// (theme-m3.css) as the sidebar's own indirection layer, consumed by MainLayout.razor.css/
// NavMenu.razor.css instead of --m3-primary/--m3-on-primary directly -- light theme keeps the
// pre-existing bold brand-color sidebar unchanged (identical formulas), only dark theme's
// treatment actually changes. Reads the CSS files as plain text, same convention as every other
// Styling/ test (no CSS cascade/computed-style resolution possible in bUnit).
public class SidebarDarkThemeLuminanceTests
{
    private static string ThemeCss { get; } = File.ReadAllText(RepoPath("src", "ExcelETL.BlazorAdmin", "wwwroot", "css", "theme-m3.css"));
    private static string MainLayoutCss { get; } = File.ReadAllText(RepoPath("src", "ExcelETL.BlazorAdmin", "Components", "Layout", "MainLayout.razor.css"));
    private static string NavMenuCss { get; } = File.ReadAllText(RepoPath("src", "ExcelETL.BlazorAdmin", "Components", "Layout", "NavMenu.razor.css"));

    [Fact]
    public void DarkTheme_SidebarBackground_IsTheNearBlackAppBackground_NotTheBrightPrimary()
    {
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        darkBlock.Should().Contain("--m3-sidebar-bg-start: var(--m3-background);");
        darkBlock.Should().Contain("--m3-sidebar-bg-end: var(--m3-background);");
        darkBlock.Should().Contain("--m3-sidebar-fg: var(--m3-on-background);");
    }

    [Fact]
    public void DarkTheme_ActiveNavItem_UsesPrimaryAsATextAccent_NotASolidFill()
    {
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        darkBlock.Should().Contain("--m3-sidebar-active-fg: var(--m3-primary);");
        // A faint tint, not a solid fill -- "16%" is the specific value chosen, asserted so a
        // future edit can't silently turn this back into a dominant block of color.
        darkBlock.Should().Contain("--m3-sidebar-active-bg: color-mix(in srgb, var(--m3-primary) 16%, transparent);");
    }

    [Fact]
    public void LightTheme_Sidebar_KeepsTheExactPreExistingBrandGradient_NoVisualChange()
    {
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");

        lightBlock.Should().Contain("--m3-sidebar-bg-start: var(--m3-primary);");
        lightBlock.Should().Contain("--m3-sidebar-bg-end: color-mix(in srgb, var(--m3-primary) 65%, black);");
        lightBlock.Should().Contain("--m3-sidebar-fg: var(--m3-on-primary);");
    }

    [Fact]
    public void MainLayoutSidebarRule_ConsumesTheSidebarTokens_NotM3PrimaryDirectly()
    {
        var sidebarRule = ExtractRule(MainLayoutCss, ".sidebar {");

        sidebarRule.Should().Contain("var(--m3-sidebar-bg-start)");
        sidebarRule.Should().Contain("var(--m3-sidebar-bg-end)");
        sidebarRule.Should().Contain("var(--m3-sidebar-fg)");
        sidebarRule.Should().NotContain("--m3-primary");
        sidebarRule.Should().NotContain("--m3-on-primary");
    }

    [Fact]
    public void NavMenuCss_NoLongerReferencesM3OnPrimaryDirectly()
    {
        // Every consumer must go through the --m3-sidebar-* indirection so dark theme's different
        // treatment actually applies everywhere in the sidebar, not just on the root fill.
        NavMenuCss.Should().NotContain("--m3-on-primary");
    }

    private static string ExtractBlock(string css, string blockStartMarker)
    {
        var startIndex = css.IndexOf(blockStartMarker, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"the block starting with '{blockStartMarker}' should exist");

        var openBraceIndex = css.IndexOf('{', startIndex);
        var closeBraceIndex = css.IndexOf('}', openBraceIndex);

        return css[openBraceIndex..(closeBraceIndex + 1)];
    }

    private static string ExtractRule(string css, string selectorMarker)
    {
        var startIndex = css.IndexOf(selectorMarker, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"the rule '{selectorMarker}' should exist");

        var closeBraceIndex = css.IndexOf('}', startIndex);
        return css[startIndex..(closeBraceIndex + 1)];
    }

    private static string RepoPath(params string[] segments)
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

        return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
    }
}
