using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// QuickGrid pads the visible page out to Pagination.ItemsPerPage (25) with empty filler <tr>s (no
// aria-rowindex) so the table's height never shrinks on a partial last page -- confirmed by
// decompiling the installed package. With most real pages far under 25 rows, this rendered as a
// long band of near-invisible empty striped rows below the real data (client-reported). Hidden via
// a scoped ::deep rule (QuickGrid renders its own <table> from a child component, unreachable by
// GeneratedFiles.razor.css's own scope attribute without ::deep). Same plain-text-CSS-read idiom
// as MainLayoutRazorCssTopRowColorTests -- bUnit computes no CSS, so this asserts the rule exists
// and targets exactly the non-data rows, not that it visually renders correctly.
public class GeneratedFilesGridFillerRowVisibilityTests
{
    private static string GeneratedFilesCss { get; } = File.ReadAllText(GeneratedFilesCssPath());

    [Fact]
    public void HidesOnlyRowsWithoutAriaRowindex_WithinTheGridContainer()
    {
        GeneratedFilesCss.Should().Contain("::deep #generated-files-grid");
        GeneratedFilesCss.Should().Contain("tr:not([aria-rowindex])");
        GeneratedFilesCss.Should().Contain("display: none");
    }

    [Fact]
    public void NeverHidesRowsThatCarryAriaRowindex()
    {
        // A bare `tr[aria-rowindex] { display: none; }` (or similar) would defeat the whole fix --
        // guard against that specific inversion, not just presence of the right-looking substrings.
        GeneratedFilesCss.Should().NotContain("tr[aria-rowindex] {");
        GeneratedFilesCss.Should().NotContain("tr[aria-rowindex]{");
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

    private static string GeneratedFilesCssPath() => Path.Combine(
        RepositoryRoot(), "src", "ExcelETL.BlazorAdmin", "Components", "Pages", "Admin", "GeneratedFiles.razor.css");
}
