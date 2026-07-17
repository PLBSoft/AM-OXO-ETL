using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

// Runs ProcedureExtractionService (Application, Lot C1) against the real ClosedXmlWorkbookReader
// (Infrastructure, Lot E1) and the 3 real client fixtures, per the Lot C ticket's requirement that
// each per-sheet service be validated against real files, not just Mock<IWorkbookReader>. Lives in
// Infrastructure.Tests (rather than Application.Tests) so it can use ClosedXmlWorkbookReader without
// Application.Tests needing a reference to Infrastructure.
public class ProcedureExtractionServiceIntegrationTests
{
    private const string Sheet = "PROCEDURE";
    private const string ReperePrefix = "MAD-OXO-";
    private const string EquipementTypeElementNom = "MAD TRAVAUX";

    private readonly ProcedureExtractionService _sut = new(new TextTransformEvaluator());

    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 9, 1, ProcedureFieldNames.Action,
        [
            new BlockFieldDefinition(ProcedureFieldNames.Action, "C:L", 0, 0),
            new BlockFieldDefinition(ProcedureFieldNames.Ordre, "B", 0, 0),
            new BlockFieldDefinition(ProcedureFieldNames.Acteur, "M:N", 0, 0),
            new BlockFieldDefinition(ProcedureFieldNames.Risques, "O:Q", 0, 0),
            new BlockFieldDefinition(ProcedureFieldNames.TypeTacheMultipleAlias, "R", 0, 0),
            new BlockFieldDefinition(ProcedureFieldNames.DateValidation, "T:U", 0, 0)
        ]),
        [],
        []);

    [Fact]
    public void Extract_C7401Fixture_ReturnsExpectedEquipementAndFirstTaskRows()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Errors.Should().BeEmpty();
        result.Equipement.Should().NotBeNull();
        result.Equipement!.Repere.Should().Be("38-C7401");
        result.Equipement.Designation.Should().Be("Rév 2 du 12/12/2025");
        result.Equipement.TypeElementNom.Should().Be(EquipementTypeElementNom);
        result.Points.Should().BeEquivalentTo(
        [
            new PointPivot("TRAVAUX COMPLET", "38-C7401"),
            new PointPivot("TRAVAUX DETAIL", "38-C7401")
        ]);

        result.TachesMultiples.Should().HaveCount(98);
        result.TachesMultiples[0].EstFactice.Should().BeTrue();
        result.TachesMultiples[0].Action.Should().Be("1-MANOEUVRES PREPARATOIRES");
        result.TachesMultiples[1].Ordre.Should().Be(1);
        result.TachesMultiples[1].EstFactice.Should().BeFalse();
    }

    [Fact]
    public void Extract_D8570Fixture_ReturnsExpectedEquipementAndFirstTaskRows()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        result.Errors.Should().BeEmpty();
        result.Equipement.Should().NotBeNull();
        result.Equipement!.Repere.Should().Be("644-D8570");
        result.Equipement.Designation.Should().Be("Rév 0 du 11/09/2025");
        result.Equipement.TypeElementNom.Should().Be(EquipementTypeElementNom);

        result.TachesMultiples.Should().NotBeEmpty();
        result.TachesMultiples[0].EstFactice.Should().BeTrue();
        result.TachesMultiples[0].Action.Should().Be("ARRET COLONNE");
        result.TachesMultiples[1].Ordre.Should().Be(1);
    }

    [Fact]
    public void Extract_G6306BFixture_ReturnsExpectedEquipementAndFirstTaskRows()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        result.Errors.Should().BeEmpty();
        result.Equipement.Should().NotBeNull();
        result.Equipement!.Repere.Should().Be("602-G6306B");
        result.Equipement.Designation.Should().Be("Rév 0 du 12/05/2025");
        result.Equipement.TypeElementNom.Should().Be(EquipementTypeElementNom);

        result.TachesMultiples.Should().NotBeEmpty();
        result.TachesMultiples[0].Ordre.Should().Be(1);
        result.TachesMultiples[0].EstFactice.Should().BeFalse();
    }

    private ImportResult ExtractFromFixture(string fileName)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _sut.Extract(workbookReader, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom);
    }

    private static string FixturePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Fixtures")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the tests/Fixtures directory.");
        }

        return Path.Combine(directory.FullName, "Fixtures", fileName);
    }
}
