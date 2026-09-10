using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Client-reported 404 on the real deployed site: App.razor originally hardcoded
// href="_content/Microsoft.AspNetCore.Components.QuickGrid/QuickGrid.css" -- a file that does not
// exist in this package version at all (confirmed by inspecting the installed nupkg's own
// staticwebassets folder: only QuickGrid.razor.js and the package's own Blazor CSS-isolation
// bundle, Microsoft.AspNetCore.Components.QuickGrid.bundle.scp.css, ship there). Guards both the
// wrong filename never reappearing and the right one being routed through @Assets[...] like every
// other stylesheet in this file -- a hardcoded href bypasses app.MapStaticAssets()'s fingerprinted
// URLs (Program.cs has no UseStaticFiles()), confirmed via the generated
// staticwebassets.publish.endpoints.json. Same plain-text-read convention as
// ThemeToggleAppHeadOrderTests -- rendered/computed output can't prove a stylesheet actually loads
// without a real browser.
public class AppQuickGridStylesheetLinkTests
{
    private static string AppRazor { get; } = File.ReadAllText(AppRazorPath());

    [Fact]
    public void NeverReferencesTheNonexistentQuickGridCssFile()
    {
        // Scoped to the actual href attribute, not a bare substring search -- this file's own
        // explanatory comment mentions the literal text "QuickGrid.css" (documenting the mistake),
        // which a whole-file Contains check would false-positive on.
        AppRazor.Should().NotContain("href=\"_content/Microsoft.AspNetCore.Components.QuickGrid/QuickGrid.css\"");
    }

    [Fact]
    public void ReferencesTheRealScopedCssBundle_ThroughTheAssetsIndexer()
    {
        AppRazor.Should().Contain(
            "@Assets[\"_content/Microsoft.AspNetCore.Components.QuickGrid/" +
            "Microsoft.AspNetCore.Components.QuickGrid.bundle.scp.css\"]");
    }

    private static string AppRazorPath()
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

        return Path.Combine(directory.FullName, "src", "ExcelETL.BlazorAdmin", "Components", "App.razor");
    }
}
