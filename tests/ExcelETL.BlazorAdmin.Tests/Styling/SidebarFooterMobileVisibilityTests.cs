using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Follow-up (post-062, client feedback on the deployed logo/version sidebar footer): the footer
// (client logo + version/date) is hidden entirely on mobile -- the same information is shown on
// Home.razor instead there -- and, on desktop, pushed to the true bottom of the sidebar via
// margin-top:auto rather than merely sitting right after the last nav link (which left a visible
// gap under it whenever the current account's link list was short). bUnit computes no CSS/layout
// at all, so this reuses the Styling/ folder's own "read the .razor.css as plain text" convention
// (e.g. MainLayoutRazorCssTopRowColorTests) rather than a bUnit assertion that couldn't prove it.
public class SidebarFooterMobileVisibilityTests
{
    private static string NavMenuCss { get; } = File.ReadAllText(RepoPath("src", "ExcelETL.BlazorAdmin", "Components", "Layout", "NavMenu.razor.css"));
    private static string HomeCss { get; } = File.ReadAllText(RepoPath("src", "ExcelETL.BlazorAdmin", "Components", "Pages", "Home.razor.css"));

    [Fact]
    public void SidebarFooter_IsHiddenByDefault_ForMobile()
    {
        NavMenuCss.Should().MatchRegex(@"\.sidebar-footer\s*\{\s*display:\s*none;\s*\}");
    }

    [Fact]
    public void SidebarFooter_IsShownAsAFlexColumn_PushedToTheBottom_AtTheDesktopBreakpoint()
    {
        // Same 641px breakpoint the rest of the sidebar already switches on (MainLayout.razor.css's
        // own .sidebar/.nav-scrollable rules), not Bootstrap's 768px `md`.
        NavMenuCss.Should().MatchRegex(
            @"@media \(min-width: 641px\)\s*\{[\s\S]*?\.sidebar-footer\s*\{[\s\S]*?display:\s*flex;[\s\S]*?margin-top:\s*auto;[\s\S]*?\}[\s\S]*?\}");
    }

    [Fact]
    public void NavScrollableNav_FillsItsContainerHeight_AtTheDesktopBreakpoint_SoTheFooterHasSpareSpaceToBePushedInto()
    {
        NavMenuCss.Should().MatchRegex(
            @"@media \(min-width: 641px\)\s*\{[\s\S]*?\.nav-scrollable > nav\.nav\.flex-column\s*\{[\s\S]*?min-height:\s*100%;[\s\S]*?\}[\s\S]*?\}");
    }

    [Fact]
    public void HomeMobileBrand_IsHiddenByDefault_ForDesktop()
    {
        HomeCss.Should().MatchRegex(@"\.home-mobile-brand\s*\{\s*display:\s*none;\s*\}");
    }

    [Fact]
    public void HomeMobileBrand_IsShown_AtTheSameMobileBreakpointTheSidebarFooterHidesAt()
    {
        // The inverse rule of SidebarFooter_IsHiddenByDefault_ForMobile -- exactly one of the two
        // locations shows this information at any given viewport width.
        HomeCss.Should().MatchRegex(
            @"@media \(max-width: 640\.98px\)\s*\{[\s\S]*?\.home-mobile-brand\s*\{[\s\S]*?display:\s*flex;[\s\S]*?\}[\s\S]*?\}");
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

        return Path.Combine([directory.FullName, .. segments]);
    }
}
