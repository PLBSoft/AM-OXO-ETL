using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Real bug found while investigating a client-reported color defect (2026-07-31): one comment in
// the [data-bs-theme="dark"] block was opened with `/*` but closed with the HTML-comment marker
// `-->` instead of `*/` -- CSS doesn't recognize `-->` as a comment terminator, so the comment
// silently swallowed the next 3 declarations (--m3-secondary-container/-rgb/--m3-on-secondary-
// container) until the next real `*/`. Those custom properties then fell back to the *light*
// theme's values (declared at :root, same specificity, earlier in source order) even under
// [data-bs-theme="dark"] -- which is exactly why `.btn-secondary` ("Ajouter la colonne" etc.)
// rendered with the light theme's bright cyan container color while the rest of the page was dark.
// This test is a permanent guard-rail: every `/*` in the file must be balanced by a real `*/`,
// and no literal `-->` may appear at all. Not testable in bUnit (no CSS parser there either) --
// reads the file as plain text, same convention as the other Styling/ tests.
public class ThemeM3CssCommentIntegrityTests
{
    private static string ThemeCss { get; } = File.ReadAllText(ThemeM3CssPath());

    [Fact]
    public void ContainsNoHtmlStyleCommentCloser()
    {
        ThemeCss.Should().NotContain("-->", "a CSS comment must close with */, not the HTML comment marker -->");
    }

    [Fact]
    public void EveryCommentOpener_HasAMatchingCloser()
    {
        var openers = CountOccurrences(ThemeCss, "/*");
        var closers = CountOccurrences(ThemeCss, "*/");

        closers.Should().Be(openers, "every /* must be closed by a real */ -- an imbalance means a comment silently swallowed real CSS");
    }

    [Fact]
    public void DarkBlock_DeclaresItsOwnSecondaryContainerTokens_DistinctFromTheLightBlock()
    {
        // The regression this bug actually caused: the dark block's own values for these 3 tokens
        // must exist and differ from light's -- if they were ever swallowed by a comment again,
        // the dark block would simply not declare them and this assertion would catch it.
        var lightBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"light\"] {");
        var darkBlock = ExtractBlock(ThemeCss, "[data-bs-theme=\"dark\"] {");

        foreach (var token in new[] { "--m3-secondary-container", "--m3-secondary-container-rgb", "--m3-on-secondary-container" })
        {
            darkBlock.Should().Contain($"{token}:", $"'{token}' must be declared inside the dark block");

            var lightValue = ExtractValue(lightBlock, token);
            var darkValue = ExtractValue(darkBlock, token);
            darkValue.Should().NotBe(lightValue, $"'{token}' should have a distinct dark-theme value, not silently fall back to light's");
        }
    }

    private static int CountOccurrences(string text, string marker)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(marker, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += marker.Length;
        }

        return count;
    }

    private static string ExtractBlock(string css, string blockStartMarker)
    {
        var startIndex = css.IndexOf(blockStartMarker, StringComparison.Ordinal);
        startIndex.Should().BeGreaterThanOrEqualTo(0, $"the block starting with '{blockStartMarker}' should exist in theme-m3.css");

        var openBraceIndex = css.IndexOf('{', startIndex);
        var closeBraceIndex = css.IndexOf('}', openBraceIndex);

        return css[openBraceIndex..(closeBraceIndex + 1)];
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

    private static string ThemeM3CssPath()
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

        return Path.Combine(directory.FullName, "src", "ExcelETL.BlazorAdmin", "wwwroot", "css", "theme-m3.css");
    }
}
