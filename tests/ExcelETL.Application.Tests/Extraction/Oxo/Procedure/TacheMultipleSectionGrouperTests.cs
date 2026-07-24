using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Domain.Extraction.Pivot;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo.Procedure;

// Lot 032 (docs/tickets-tdd-lot-032-detection-incoherence-type-procedure.md), note d'efficacité #4:
// no existing mechanism groups PROCEDURE's flat TacheMultiplePivot list into sections, so this is
// built and tested as a discrete step before the type-coherence analyzer (32.1) consumes it.
public class TacheMultipleSectionGrouperTests
{
    private static TacheMultiplePivot Factice(string title) =>
        new(null, title, "", "", "", null, estFactice: true);

    private static TacheMultiplePivot Tache(int ordre, string type) =>
        new(ordre, "Some action", "", "", type, null, estFactice: false);

    [Fact]
    public void GroupBySection_WithEmptyList_ReturnsNoSections()
    {
        var result = TacheMultipleSectionGrouper.GroupBySection([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GroupBySection_WithOneFacticeHeaderAndTasks_ReturnsOneSectionWithTitleAndOrderedTasks()
    {
        var tachesMultiples = new List<TacheMultiplePivot>
        {
            Factice("1-SECTION"),
            Tache(1, "TM_PROC_MAD"),
            Tache(2, "TM_PROC_MAD")
        };

        var result = TacheMultipleSectionGrouper.GroupBySection(tachesMultiples);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("1-SECTION");
        result[0].Tasks.Should().Equal((1, "TM_PROC_MAD"), (2, "TM_PROC_MAD"));
    }

    [Fact]
    public void GroupBySection_WithMultipleFacticeHeaders_ReturnsOneSectionPerHeaderInOrder()
    {
        var tachesMultiples = new List<TacheMultiplePivot>
        {
            Factice("1-SECTION"),
            Tache(1, "TM_PROC_MAD"),
            Factice("2-SECTION"),
            Tache(2, "TM_PROC_REL"),
            Tache(3, "TM_PROC_REL")
        };

        var result = TacheMultipleSectionGrouper.GroupBySection(tachesMultiples);

        result.Should().HaveCount(2);
        result[0].Title.Should().Be("1-SECTION");
        result[0].Tasks.Should().Equal((1, "TM_PROC_MAD"));
        result[1].Title.Should().Be("2-SECTION");
        result[1].Tasks.Should().Equal((2, "TM_PROC_REL"), (3, "TM_PROC_REL"));
    }

    [Fact]
    public void GroupBySection_WithFacticeHeaderFollowedImmediatelyByAnotherHeader_ProducesNoEmptySection()
    {
        var tachesMultiples = new List<TacheMultiplePivot>
        {
            Factice("1-EMPTY SECTION"),
            Factice("2-SECTION"),
            Tache(1, "TM_PROC_MAD")
        };

        var result = TacheMultipleSectionGrouper.GroupBySection(tachesMultiples);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("2-SECTION");
    }

    [Fact]
    public void GroupBySection_WithTasksBeforeAnyFacticeHeader_ReturnsSectionWithEmptyTitle()
    {
        // Real-world case: the G6306B fixture's PROCEDURE sheet starts with a non-factice task, no
        // section header before it (ProcedureExtractionServiceIntegrationTests.
        // Extract_G6306BFixture_ReturnsExpectedEquipementAndFirstTaskRows).
        var tachesMultiples = new List<TacheMultiplePivot> { Tache(1, "TM_PROC_MAD") };

        var result = TacheMultipleSectionGrouper.GroupBySection(tachesMultiples);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("");
        result[0].Tasks.Should().Equal((1, "TM_PROC_MAD"));
    }

    [Fact]
    public void GroupBySection_WithNonFacticeTaskHavingUnparsableOrdre_ExcludesItWithoutThrowing()
    {
        // Pre-existing edge case in ProcedureExtractionService.ReadTachesMultiples: a non-blank but
        // unparsable Ordre cell means EstFactice = false yet Ordre stays null. Out of this ticket's
        // scope to fix -- just must not crash the new section grouping.
        var tacheWithUnparsableOrdre = new TacheMultiplePivot(null, "Some action", "", "", "TM_PROC_MAD", null, estFactice: false);
        var tachesMultiples = new List<TacheMultiplePivot>
        {
            Factice("1-SECTION"),
            tacheWithUnparsableOrdre,
            Tache(1, "TM_PROC_MAD")
        };

        var result = TacheMultipleSectionGrouper.GroupBySection(tachesMultiples);

        result.Should().ContainSingle();
        result[0].Tasks.Should().Equal((1, "TM_PROC_MAD"));
    }
}
