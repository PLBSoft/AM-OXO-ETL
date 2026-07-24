using ExcelETL.Application.Generation;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Application.Tests.Generation;

public class SheetGenerationEngineTests
{
    private readonly SheetGenerationEngine _sut = new(NullLogger<SheetGenerationEngine>.Instance);

    private static SheetGenerationRule ParentsRule() => new(
        "Parents",
        PivotSource.Equipement,
        [
            new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere),
            new ColumnDefinition("Désignation", PivotFieldRef.EquipementDesignation),
            new ColumnDefinition("Colonne libre", null)
        ],
        [
            new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet"),
            new PointColumnDefinition("TRAVAUX DETAIL", "Travaux détail")
        ],
        []);

    private static SheetGenerationRule EnfantsRule() => new(
        "Enfants",
        PivotSource.Isolement,
        [
            new ColumnDefinition("Repère", PivotFieldRef.IsolementRepere),
            new ColumnDefinition("Désignation", PivotFieldRef.IsolementDesignation)
        ],
        [new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes")],
        []);

    private static SheetGenerationRule TachesMultiplesRule() => new(
        "Tâches multiples",
        PivotSource.TacheMultiple,
        [
            new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre),
            new ColumnDefinition("Action", PivotFieldRef.TacheMultipleAction),
            new ColumnDefinition("Acteur", PivotFieldRef.TacheMultipleActeur),
            new ColumnDefinition("Risques", PivotFieldRef.TacheMultipleRisques),
            new ColumnDefinition("Date de validation", PivotFieldRef.TacheMultipleDateValidation)
        ],
        [],
        []);

    private static ImportResult ImportResultWith(params TacheMultiplePivot[] tachesMultiples) => new(
        new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"), [], [], tachesMultiples, []);

    [Fact]
    public void Generate_ForEquipementSheet_WritesHeaderInDescriptiveThenPointOrder()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRule()]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"), [], [], [], []);

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets.Should().ContainSingle();
        workbook.Sheets[0].Name.Should().Be("Parents");
        workbook.Sheets[0].Headers.Should().Equal("Repère", "Désignation", "Colonne libre", "Travaux complet", "Travaux détail");
    }

    [Fact]
    public void Generate_ForEquipementSheet_WritesOneRowMarkingOnlyMatchingPoints()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRule()]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"),
            [],
            [new PointPivot("TRAVAUX COMPLET", "38-C7401")],
            [],
            []);

        var workbook = _sut.Generate(importResult, profile);

        var row = workbook.Sheets[0].Rows.Should().ContainSingle().Which;
        row.Cells.Should().Equal("38-C7401", "Compresseur C7401", "", "X", "");
    }

    [Fact]
    public void Generate_ForIsolementSheet_WritesOneRowPerIsolement()
    {
        var profile = new ExportProfile("Profil export test", [EnfantsRule()]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"),
            [
                new IsolementPivot("C7401-V1", "Vanne 1", "VANNE", "MAD", ""),
                new IsolementPivot("C7401-V2", "Vanne 2", "VANNE", "MAD", "")
            ],
            [new PointPivot("PROLOCK VANNES", "C7401-V1")],
            [],
            []);

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets[0].Rows.Should().HaveCount(2);
        workbook.Sheets[0].Rows[0].Cells.Should().Equal("C7401-V1", "Vanne 1", "X");
        workbook.Sheets[0].Rows[1].Cells.Should().Equal("C7401-V2", "Vanne 2", "");
    }

    [Fact]
    public void Generate_WithNullColumnSource_WritesEmptyCellWithoutThrowing()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRule()]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"), [], [], [], []);

        var act = () => _sut.Generate(importResult, profile);

        act.Should().NotThrow();
        act().Sheets[0].Rows[0].Cells[2].Should().Be("");
    }

    [Fact]
    public void Generate_WhenEquipementIsNull_ProducesSheetWithHeaderButNoRows()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRule()]);
        var importResult = new ImportResult(
            null, [], [], [], [new ExtractionError("PROCEDURE", "M2:O2", ExtractionErrorCode.RequiredFieldMissing, "vide")]);

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets.Should().ContainSingle();
        workbook.Sheets[0].Headers.Should().NotBeEmpty();
        workbook.Sheets[0].Rows.Should().BeEmpty();
    }

    [Fact]
    public void Generate_WithMultipleSheetRules_ProducesSheetsInProfileOrder()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRule(), EnfantsRule()]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"), [], [], [], []);

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets.Should().HaveCount(2);
        workbook.Sheets[0].Name.Should().Be("Parents");
        workbook.Sheets[1].Name.Should().Be("Enfants");
    }

    [Fact]
    public void Generate_ForTacheMultipleRule_ProducesOnePhysicalSheetPerDistinctCode()
    {
        var profile = new ExportProfile("Profil export test", [TachesMultiplesRule()]);
        var importResult = ImportResultWith(
            new TacheMultiplePivot(1, "Consigner", "ADF", "Aucun", "TM_PROC_MAD", new DateOnly(2026, 7, 20), false),
            new TacheMultiplePivot(1, "Déconsigner", "ADF", "Aucun", "TM_PROC_REL", new DateOnly(2026, 7, 21), false));

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets.Should().HaveCount(2);
        workbook.Sheets.Select(sheet => sheet.Name).Should().Equal("TM_PROC_MAD", "TM_PROC_REL");
    }

    [Fact]
    public void Generate_ForTacheMultipleRule_WritesCorrectColumnValuesIncludingFactice()
    {
        var profile = new ExportProfile("Profil export test", [TachesMultiplesRule()]);
        var importResult = ImportResultWith(
            new TacheMultiplePivot(1, "Consigner", "ADF", "Aucun", "TM_PROC_MAD", new DateOnly(2026, 7, 20), false),
            new TacheMultiplePivot(null, "--- Section suivante ---", "", "", "TM_PROC_MAD", null, true));

        var workbook = _sut.Generate(importResult, profile);

        var sheet = workbook.Sheets.Should().ContainSingle().Which;
        sheet.Headers.Should().Equal("Ordre", "Action", "Acteur", "Risques", "Date de validation");
        sheet.Rows.Should().HaveCount(2);
        sheet.Rows[0].Cells.Should().Equal("1", "Consigner", "ADF", "Aucun", "20/07/2026");
        sheet.Rows[1].Cells.Should().Equal("", "--- Section suivante ---", "", "", "");
    }

    [Fact]
    public void Generate_ForTacheMultipleRule_PreservesRowOrderRatherThanSortingByOrdre()
    {
        var profile = new ExportProfile("Profil export test", [TachesMultiplesRule()]);
        var importResult = ImportResultWith(
            new TacheMultiplePivot(1, "Première", "ADF", "Aucun", "TM_PROC_MAD", null, false),
            new TacheMultiplePivot(null, "Factice intercalée", "", "", "TM_PROC_MAD", null, true),
            new TacheMultiplePivot(2, "Deuxième", "ADF", "Aucun", "TM_PROC_MAD", null, false));

        var workbook = _sut.Generate(importResult, profile);

        var sheet = workbook.Sheets.Should().ContainSingle().Which;
        sheet.Rows.Select(row => row.Cells[1]).Should().Equal("Première", "Factice intercalée", "Deuxième");
    }

    [Fact]
    public void Generate_ForTacheMultipleRule_SortsGeneratedSheetsAlphabeticallyByCode()
    {
        var profile = new ExportProfile("Profil export test", [TachesMultiplesRule()]);
        var importResult = ImportResultWith(
            new TacheMultiplePivot(1, "Z action", "ADF", "", "TM_PROC_Z", null, false),
            new TacheMultiplePivot(1, "A action", "ADF", "", "TM_PROC_A", null, false),
            new TacheMultiplePivot(1, "M action", "ADF", "", "TM_PROC_M", null, false));

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets.Select(sheet => sheet.Name).Should().Equal("TM_PROC_A", "TM_PROC_M", "TM_PROC_Z");
    }

    [Fact]
    public void Generate_ForTacheMultipleRule_WhenTachesMultiplesEmpty_ProducesNoTacheMultipleSheet()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRule(), TachesMultiplesRule()]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"), [], [], [], []);

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets.Should().ContainSingle();
        workbook.Sheets[0].Name.Should().Be("Parents");
    }

    [Fact]
    public void Generate_WithTacheMultipleSheetAfterParentsAndEnfants_KeepsThemFirst()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRule(), EnfantsRule(), TachesMultiplesRule()]);
        var importResult = ImportResultWith(
            new TacheMultiplePivot(1, "Consigner", "ADF", "Aucun", "TM_PROC_MAD", null, false));

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets.Should().HaveCount(3);
        workbook.Sheets[0].Name.Should().Be("Parents");
        workbook.Sheets[1].Name.Should().Be("Enfants");
        workbook.Sheets[2].Name.Should().Be("TM_PROC_MAD");
    }

    [Fact]
    public void Generate_ForTacheMultipleRule_SanitizesForbiddenCharactersInSheetName()
    {
        var profile = new ExportProfile("Profil export test", [TachesMultiplesRule()]);
        var importResult = ImportResultWith(
            new TacheMultiplePivot(1, "Consigner", "ADF", "Aucun", "TM/PROC:MAD", null, false));

        var act = () => _sut.Generate(importResult, profile);

        act.Should().NotThrow();
        act().Sheets[0].Name.Should().Be("TM_PROC_MAD");
    }

    [Fact]
    public void Generate_ForTacheMultipleRule_TruncatesSheetNameLongerThan31Characters()
    {
        var longCode = new string('A', 40);
        var profile = new ExportProfile("Profil export test", [TachesMultiplesRule()]);
        var importResult = ImportResultWith(
            new TacheMultiplePivot(1, "Consigner", "ADF", "Aucun", longCode, null, false));

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets[0].Name.Should().HaveLength(31);
        workbook.Sheets[0].Name.Should().Be(longCode[..31]);
    }

    [Fact]
    public void Generate_ForTacheMultipleRule_DoesNotModifyKnownRealCodes()
    {
        var profile = new ExportProfile("Profil export test", [TachesMultiplesRule()]);
        var importResult = ImportResultWith(
            new TacheMultiplePivot(1, "Consigner", "ADF", "Aucun", "TM_PROC_MAD", null, false),
            new TacheMultiplePivot(1, "Déconsigner", "ADF", "Aucun", "TM_PROC_REL", null, false));

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets.Select(sheet => sheet.Name).Should().Equal("TM_PROC_MAD", "TM_PROC_REL");
    }

    // Lot U (docs/tickets-tdd-pivot-tableaux-applications-export.md), U5: Tableaux (a plain
    // ColumnDefinition rendering a comma-joined list via PivotFieldResolver, no engine change needed --
    // see U4) and Applications (a new dedicated column kind, tested here).
    private static SheetGenerationRule ParentsRuleWithTableauxAndApplicationColumns() => new(
        "Parents",
        PivotSource.Equipement,
        [
            new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere),
            new ColumnDefinition("Tableaux", PivotFieldRef.EquipementTableaux)
        ],
        [],
        [new ApplicationColumnDefinition("PROGRESS", "PROGRESS", "O")]);

    [Fact]
    public void Generate_ForEquipementSheet_RendersTableauxColumnAsCommaJoinedList()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRuleWithTableauxAndApplicationColumns()]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX")
                with { Tableaux = ["TRAVAUX COMPLET", "TRAVAUX DETAIL"] },
            [], [], [], []);

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets[0].Rows.Should().ContainSingle().Which.Cells[1].Should().Be("TRAVAUX COMPLET, TRAVAUX DETAIL");
    }

    [Fact]
    public void Generate_ForEquipementSheet_WithEmptyTableaux_RendersEmptyCellWithoutThrowing()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRuleWithTableauxAndApplicationColumns()]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"), [], [], [], []);

        var act = () => _sut.Generate(importResult, profile);

        act.Should().NotThrow();
        act().Sheets[0].Rows[0].Cells[1].Should().Be("");
    }

    [Fact]
    public void Generate_ForEquipementSheet_WritesHeaderInDescriptiveThenApplicationThenPointOrder()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRuleWithTableauxAndApplicationColumns()]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"), [], [], [], []);

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets[0].Headers.Should().Equal("Repère", "Tableaux", "PROGRESS");
    }

    [Fact]
    public void Generate_ForEquipementSheet_MarksApplicationColumnWhenPivotApplicationsContainsName()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRuleWithTableauxAndApplicationColumns()]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX") with { Applications = ["PROGRESS"] },
            [], [], [], []);

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets[0].Rows.Should().ContainSingle().Which.Cells[2].Should().Be("O");
    }

    [Fact]
    public void Generate_ForEquipementSheet_LeavesApplicationColumnEmptyWhenPivotApplicationsDoesNotContainName()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRuleWithTableauxAndApplicationColumns()]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX") with { Applications = ["AUTRE_APP"] },
            [], [], [], []);

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets[0].Rows.Should().ContainSingle().Which.Cells[2].Should().Be("");
    }

    [Fact]
    public void Generate_ForEquipementSheet_MarksApplicationColumn_TrimmedAndCaseInsensitive()
    {
        var profile = new ExportProfile("Profil export test", [ParentsRuleWithTableauxAndApplicationColumns()]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX") with { Applications = ["progress "] },
            [], [], [], []);

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets[0].Rows.Should().ContainSingle().Which.Cells[2].Should().Be("O");
    }

    [Fact]
    public void Generate_ForIsolementSheet_MarksApplicationColumnWhenPivotApplicationsContainsName()
    {
        var rule = new SheetGenerationRule(
            "Enfants", PivotSource.Isolement,
            [new ColumnDefinition("Repère", PivotFieldRef.IsolementRepere)],
            [],
            [new ApplicationColumnDefinition("PROGRESS", "PROGRESS", "O")]);
        var profile = new ExportProfile("Profil export test", [rule]);
        var importResult = new ImportResult(
            new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX"),
            [new IsolementPivot("C7401-V1", "Vanne 1", "VANNE", "MAD", "") with { Applications = ["PROGRESS"] }],
            [], [], []);

        var workbook = _sut.Generate(importResult, profile);

        workbook.Sheets[0].Rows.Should().ContainSingle().Which.Cells[1].Should().Be("O");
    }
}
