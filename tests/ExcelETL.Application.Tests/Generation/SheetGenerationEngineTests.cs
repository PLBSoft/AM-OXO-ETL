using ExcelETL.Application.Generation;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Application.Tests.Generation;

public class SheetGenerationEngineTests
{
    private readonly SheetGenerationEngine _sut = new();

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
        ]);

    private static SheetGenerationRule EnfantsRule() => new(
        "Enfants",
        PivotSource.Isolement,
        [
            new ColumnDefinition("Repère", PivotFieldRef.IsolementRepere),
            new ColumnDefinition("Désignation", PivotFieldRef.IsolementDesignation)
        ],
        [new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes")]);

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
}
