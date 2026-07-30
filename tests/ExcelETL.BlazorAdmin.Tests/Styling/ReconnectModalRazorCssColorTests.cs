using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 060 (60.1.b): ReconnectModal.razor.css is a default Blazor scaffold, never touched since, and
// carried the framework's own default blue (#6b9ed2/#3b6ea2/#0087ff) plus a hardcoded white
// background -- none of it raccorde to the app's brand or dark theme. Reuses
// ThemeSecondaryButtonContrastTests' plain-text-read idiom (no bUnit color/layout computation).
public class ReconnectModalRazorCssColorTests
{
    private static string ReconnectModalCss { get; } = File.ReadAllText(ReconnectModalCssPath());

    [Fact]
    public void ModalRoot_ReferencesModalBackgroundAndColorTokens()
    {
        var rule = ExtractRule(ReconnectModalCss, "#components-reconnect-modal {");

        rule.Should().Contain("var(--bs-modal-bg)");
        rule.Should().Contain("var(--bs-modal-color)");
    }

    [Fact]
    public void ModalButton_ReferencesPrimaryTokens_SameRoleAsBtnPrimaryElsewhere()
    {
        var rule = ExtractRule(ReconnectModalCss, "#components-reconnect-modal button {");

        rule.Should().Contain("var(--m3-primary)");
        rule.Should().Contain("var(--m3-on-primary)");
    }

    [Fact]
    public void ModalButtonHoverAndActive_ReferenceColorMixVariantsOfPrimary_NotANewLiteralHex()
    {
        var hoverRule = ExtractRule(ReconnectModalCss, "#components-reconnect-modal button:hover {");
        var activeRule = ExtractRule(ReconnectModalCss, "#components-reconnect-modal button:active {");

        hoverRule.Should().Contain("color-mix(in srgb, var(--m3-primary)");
        activeRule.Should().Contain("color-mix(in srgb, var(--m3-primary)");
    }

    [Fact]
    public void RejoiningAnimationDiv_ReferencesPrimaryToken()
    {
        var rule = ExtractRule(ReconnectModalCss, ".components-rejoining-animation div {");

        rule.Should().Contain("var(--m3-primary)");
    }

    [Fact]
    public void NoneOfTheOriginalFourHexValues_SurviveAnywhereInTheFile()
    {
        ReconnectModalCss.Should().NotContain("#6b9ed2");
        ReconnectModalCss.Should().NotContain("#3b6ea2");
        ReconnectModalCss.Should().NotContain("#0087ff");
    }

    [Fact]
    public void NoOtherHardcodedColorLiteral_RemainsInTheFile()
    {
        // Refactor step (60.1.b): verify no other hardcoded color was left behind by the initial
        // grep -- the modal's own background ("white") was exactly such a case, found and fixed
        // above. Shadow/backdrop rgba(0,0,0,...) tints are deliberately excluded: universal black
        // drop-shadows/overlays are conventional in both themes, not a theme-tracking defect.
        ReconnectModalCss.Should().NotMatchRegex(@"#[0-9A-Fa-f]{3,8}");
        ReconnectModalCss.Should().NotContain("background-color: white");
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

    private static string ReconnectModalCssPath() =>
        Path.Combine(RepositoryRoot(), "src", "ExcelETL.BlazorAdmin", "Components", "Layout", "ReconnectModal.razor.css");
}
