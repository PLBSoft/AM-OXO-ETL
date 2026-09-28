using ExcelETL.Application.Extraction.Oxo.Elements;
using ExcelETL.Domain.Extraction.Pivot;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo.Elements;

// Lot 085.3 (docs/tickets/tickets-tdd-lot-085-rejet-reperes-en-double.md).
public class DuplicateRepereDetectorTests
{
    private static IsolementPivot Element(string repere, string sheet, int row) =>
        new(repere, "Désignation", "TYPE", "", "", sourceSheetName: sheet, ligneSource: row);

    [Fact]
    public void Detect_WithoutDuplicate_ReturnsNothing()
    {
        var errors = DuplicateRepereDetector.Detect(
            [Element("LRS-V1", "ISOLEMENT", 18), Element("LRS-V2", "ISOLEMENT", 25)]);

        errors.Should().BeEmpty();
    }

    // Lot 086 (docs/tickets/tickets-tdd-lot-086-une-entree-par-repere-en-double.md): one entry per
    // duplicated repère, located on its first row; the message still names every row.
    [Fact]
    public void Detect_TwoElementsOfTheSameSheet_ReportOneEntryLocatedOnTheFirstRow()
    {
        var errors = DuplicateRepereDetector.Detect(
            [Element("LRS4504-LRS4504", "DIVERS", 18), Element("LRS4504-LRS4504", "DIVERS", 21)]);

        var error = errors.Should().ContainSingle().Which;
        error.Sheet.Should().Be("DIVERS");
        error.BlockIdentifier.Should().Be("18");
        error.Code.Should().Be(ExtractionErrorCode.DuplicateRepere);
        error.ExtractedValue.Should().Be("LRS4504-LRS4504");
    }

    [Fact]
    public void Detect_UsesTheExactMessageOfTheTicket()
    {
        var errors = DuplicateRepereDetector.Detect(
            [Element("LRS4504-LRS4504", "DIVERS", 18), Element("LRS4504-LRS4504", "DIVERS", 21)]);

        errors.Should().ContainSingle().Which.Message.Should().Be(
            "Repère « LRS4504-LRS4504 » en double (DIVERS ligne 18, DIVERS ligne 21) : " +
            "chaque élément doit avoir une identification unique.");
    }

    [Fact]
    public void Detect_SameRepereOnTwoSheets_TheEntryNamesTheFirstSheet_AndTheMessageBothRows()
    {
        var errors = DuplicateRepereDetector.Detect(
            [Element("D8570-V4", "ISOLEMENT", 116), Element("D8570-V4", "DIVERS", 30)]);

        var error = errors.Should().ContainSingle().Which;
        (error.Sheet, error.BlockIdentifier).Should().Be(("ISOLEMENT", "116"));
        error.Message.Should().Contain("(ISOLEMENT ligne 116, DIVERS ligne 30)");
    }

    [Fact]
    public void Detect_IgnoresCaseAndSurroundingSpaces_AndKeepsTheFirstRowsRepereAsExtractedValue()
    {
        var errors = DuplicateRepereDetector.Detect(
            [Element("LRS-V1", "ISOLEMENT", 18), Element(" lrs-v1 ", "DIVERS", 30)]);

        var error = errors.Should().ContainSingle().Which;
        error.ExtractedValue.Should().Be("LRS-V1");
        error.Message.Should().StartWith("Repère « LRS-V1 » en double (ISOLEMENT ligne 18, DIVERS ligne 30)");
    }

    [Fact]
    public void Detect_APrefixIsNotADuplicate()
    {
        var errors = DuplicateRepereDetector.Detect(
            [Element("LRS-V1", "ISOLEMENT", 18), Element("LRS-V10", "ISOLEMENT", 25)]);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Detect_AGroupOfThree_ReportsOneEntryNamingTheThreeRows()
    {
        var errors = DuplicateRepereDetector.Detect(
        [
            Element("X-P1", "PLATINES", 17), Element("X-P1", "PLATINES", 25), Element("X-P1", "DIVERS", 12)
        ]);

        var error = errors.Should().ContainSingle().Which;
        (error.Sheet, error.BlockIdentifier).Should().Be(("PLATINES", "17"));
        error.Message.Should().Contain("(PLATINES ligne 17, PLATINES ligne 25, DIVERS ligne 12)");
    }

    [Fact]
    public void Detect_TwoGroups_OneEntryEach_InOrderOfFirstAppearance()
    {
        var errors = DuplicateRepereDetector.Detect(
        [
            Element("X-B", "ISOLEMENT", 18),
            Element("X-A", "ISOLEMENT", 25),
            Element("X-OK", "ISOLEMENT", 32),
            Element("X-A", "PLATINES", 17),
            Element("X-B", "DIVERS", 12)
        ]);

        errors.Select(e => (e.ExtractedValue, e.Sheet, e.BlockIdentifier)).Should().Equal(
            ("X-B", "ISOLEMENT", "18"), ("X-A", "ISOLEMENT", "25"));
    }
}
