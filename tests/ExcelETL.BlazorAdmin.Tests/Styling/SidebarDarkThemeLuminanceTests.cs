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
    public void DarkTheme_SidebarBackground_IsAnElevatedToneBetweenBackgroundAndSurface_NotTheBrightPrimary()
    {
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        // Follow-up (client-reported): a flat --m3-background fill made the sidebar
        // indistinguishable from the page content behind it -- dark mode simulates elevation via a
        // slightly lighter tone, not via box-shadow. A 15% step toward --m3-surface (not all the
        // way to it, so the sidebar stays visually "behind" a card).
        darkBlock.Should().Contain("--m3-sidebar-bg-start: color-mix(in srgb, var(--m3-background) 85%, var(--m3-surface) 15%);");
        darkBlock.Should().Contain("--m3-sidebar-bg-end: color-mix(in srgb, var(--m3-background) 85%, var(--m3-surface) 15%);");
        darkBlock.Should().Contain("--m3-sidebar-fg: var(--m3-on-background);");
    }

    [Fact]
    public void DarkTheme_SidebarBackground_ComputesStrictlyBetweenBackgroundAndSurface_NotEqualToEitherEndpoint()
    {
        // A numeric guard against the elevation tone silently drifting to one of its two endpoints
        // (which would reintroduce either the "flat, indistinguishable from the page" defect this
        // fix closes, or a card-level elevation that's too close to a real .card).
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");
        var background = ParseHex(ExtractValue(darkBlock, "--m3-background"));
        var surface = ParseHex(ExtractValue(darkBlock, "--m3-surface"));
        var mixed = MixSrgb(background, surface, 0.15);

        mixed.R.Should().BeInRange(background.R + 1, surface.R - 1);
        mixed.G.Should().BeInRange(background.G + 1, surface.G - 1);
        mixed.B.Should().BeInRange(background.B + 1, surface.B - 1);
    }

    [Fact]
    public void DarkTheme_SidebarForegroundOnElevatedBackground_MeetsWcagAaContrast()
    {
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");
        var background = ParseHex(ExtractValue(darkBlock, "--m3-background"));
        var surface = ParseHex(ExtractValue(darkBlock, "--m3-surface"));
        var mixed = MixSrgb(background, surface, 0.15);
        // --m3-sidebar-fg is `var(--m3-on-background)` in the dark block.
        var foreground = ParseHex(ExtractValue(darkBlock, "--m3-on-background"));

        ContrastRatio(foreground, mixed).Should().BeGreaterThanOrEqualTo(4.5);
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

    private static string ExtractValue(string cssBlock, string variableName)
    {
        var marker = $"{variableName}:";
        var startIndex = cssBlock.IndexOf(marker, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"'{variableName}' should be declared in this block");

        var valueStart = startIndex + marker.Length;
        var valueEnd = cssBlock.IndexOf(';', valueStart);
        return cssBlock[valueStart..valueEnd].Trim();
    }

    private static (int R, int G, int B) ParseHex(string hex)
    {
        var value = hex.TrimStart('#');
        var r = Convert.ToInt32(value[..2], 16);
        var g = Convert.ToInt32(value[2..4], 16);
        var b = Convert.ToInt32(value[4..6], 16);
        return (r, g, b);
    }

    // Mirrors CSS's own `color-mix(in srgb, A p1%, B p2%)` semantics (linear per-channel blend in
    // sRGB space, not linear-light) closely enough for this test's purposes -- browsers' actual
    // `color-mix` implementation is what production relies on, this is only a numeric sanity check.
    private static (int R, int G, int B) MixSrgb((int R, int G, int B) background, (int R, int G, int B) surface, double surfaceWeight)
    {
        int Mix(int a, int b) => (int)Math.Round((a * (1 - surfaceWeight)) + (b * surfaceWeight));
        return (Mix(background.R, surface.R), Mix(background.G, surface.G), Mix(background.B, surface.B));
    }

    private static double ContrastRatio((int R, int G, int B) a, (int R, int G, int B) b)
    {
        var luminanceA = RelativeLuminance(a);
        var luminanceB = RelativeLuminance(b);

        var lighter = Math.Max(luminanceA, luminanceB);
        var darker = Math.Min(luminanceA, luminanceB);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance((int R, int G, int B) rgb)
    {
        var rLin = LinearizeChannel(rgb.R);
        var gLin = LinearizeChannel(rgb.G);
        var bLin = LinearizeChannel(rgb.B);

        return (0.2126 * rLin) + (0.7152 * gLin) + (0.0722 * bLin);
    }

    private static double LinearizeChannel(int channel8Bit)
    {
        var c = channel8Bit / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
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
