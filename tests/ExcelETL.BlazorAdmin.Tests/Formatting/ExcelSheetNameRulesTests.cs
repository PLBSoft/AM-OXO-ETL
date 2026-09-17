using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 079.6 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md): guard against
// drift with ClosedXML. The page's "blocks the generation" problems are duplicated knowledge of what
// ClosedXmlWorkbookWriter rejects; this test writes each case for real and requires both to agree.
public class ExcelSheetNameRulesTests
{
    private static SheetGenerationRule Sheet(string name, PivotSource pivotSource = PivotSource.Equipement) =>
        pivotSource == PivotSource.Isolement
            ? ExportRule(name, pivotSource, [new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere)])
            : ExportRule(name, pivotSource, [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)]);

    private static SheetGenerationRule Taches(string name) =>
        ExportRule(name, PivotSource.TacheMultiple, [new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre)]);

    public static TheoryData<string, SheetGenerationRule[]> Cases() => new()
    {
        { "32 characters", [Sheet(new string('A', 32))] },
        { "31 characters", [Sheet(new string('A', 31))] },
        { "backslash", [Sheet("A\\B")] },
        { "slash", [Sheet("A/B")] },
        { "question mark", [Sheet("A?B")] },
        { "star", [Sheet("A*B")] },
        { "opening bracket", [Sheet("A[B")] },
        { "closing bracket", [Sheet("A]B")] },
        { "colon", [Sheet("A:B")] },
        { "leading apostrophe", [Sheet("'Parents")] },
        { "trailing apostrophe", [Sheet("Parents'")] },
        { "inner apostrophe", [Sheet("Parent's")] },
        { "same name", [Sheet("Parents"), Sheet("Parents", PivotSource.Isolement)] },
        { "same name ignoring case", [Sheet("Parents"), Sheet("parents", PivotSource.Isolement)] },
        { "leading and trailing spaces", [Sheet(" Parents "), Sheet("Parents", PivotSource.Isolement)] },
        { "two task rules", [Sheet("Parents"), Taches("Tâches A"), Taches("Tâches B")] },
        { "sheet named like a task code", [Sheet("tm_proc_rel"), Taches("Tâches")] },
        { "task code name without task rule", [Sheet("TM_PROC_MAD")] },
        { "long task rule name", [Sheet("Parents"), Taches(new string('T', 40) + ":")] },
        { "standard layout", [Sheet("Parents"), Sheet("Enfants", PivotSource.Isolement), Taches("Tâches multiples")] },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void WritingTheGeneratedWorkbook_FailsExactlyWhenTheDescriptionReportsABlockingProblem(string caseName, SheetGenerationRule[] rules)
    {
        var profile = ExportProfile(rules);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Rév 1 du 01/01/2026", "MAD TRAVAUX", "PROCEDURE"),
            [new IsolementPivot("C7401-V1", "Vanne", "PROLOCK", "O", "", sourceSheetName: "ISOLEMENT")],
            [],
            [
                new TacheMultiplePivot(1, "Action MAD", "", "", "TM_PROC_MAD", null, estFactice: false, ligneSource: 9),
                new TacheMultiplePivot(2, "Action REL", "", "", "TM_PROC_REL", null, estFactice: false, ligneSource: 10)
            ],
            []);

        var reportsBlocking = Describe(profile).Sections.Any(s => s.Blocking.Count > 0);
        var workbook = new SheetGenerationEngine(NullLogger<SheetGenerationEngine>.Instance).Generate(importResult, profile);
        using var destination = new MemoryStream();
        var write = () => new ClosedXmlWorkbookWriter(NullLogger<ClosedXmlWorkbookWriter>.Instance).Write(workbook, destination);

        if (reportsBlocking)
        {
            write.Should().Throw<ArgumentException>(caseName);
        }
        else
        {
            write.Should().NotThrow(caseName);
        }
    }

    // The theory above is only meaningful if both outcomes occur.
    [Fact]
    public void Cases_CoverBothBlockingAndNonBlockingProfiles()
    {
        var outcomes = Cases().Select(row => Describe(ExportProfile((SheetGenerationRule[])row[1])).Sections.Any(s => s.Blocking.Count > 0)).ToList();

        outcomes.Should().Contain(true).And.Contain(false);
    }
}
