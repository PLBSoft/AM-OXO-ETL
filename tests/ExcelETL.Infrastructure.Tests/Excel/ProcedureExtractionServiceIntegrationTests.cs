using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
    private const string ReperePrefix = "OXO-";
    private const string EquipementTypeElementNom = "MAD TRAVAUX";
    private static readonly string[] DefaultTableaux = ["TRAVAUX COMPLET", "TRAVAUX DETAIL"];

    private readonly ProcedureExtractionService _sut =
        new(new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<ProcedureExtractionService>.Instance);

    // Lot 047: PROCEDURE's header rules, transcribed from the coordinates/template previously
    // hardcoded in ProcedureExtractionService -- same values as DefaultProfileSeeder's own seeded rule.
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
        [],
        [
            new HeaderFieldRule(ProcedureHeaderFieldNames.NomMad, new DirectCell(Sheet, "M2:O2"), stripReperePrefix: true),
            new HeaderFieldRule(ProcedureHeaderFieldNames.Revision, new DirectCell(Sheet, "P2:Q2")),
            new HeaderFieldRule(ProcedureHeaderFieldNames.DateRev, new DirectCell(Sheet, "R2:T2"), dateFormat: "dd/MM/yyyy")
        ],
        [
            new HeaderCompositeRule(
                ProcedureHeaderFieldNames.Designation,
                $"Rév {{{ProcedureHeaderFieldNames.Revision}}} du {{{ProcedureHeaderFieldNames.DateRev}}}")
        ]);

    [Fact]
    public void Extract_C7401Fixture_ReturnsExpectedEquipementAndFirstTaskRows()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

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
    public void Extract_C7401Fixture_DetectsExactlyOneTypeIncoherence_SandwichedMadRunInsideMiseEnServiceSection()
    {
        // Lot 032 (docs/tickets-tdd-lot-032-detection-incoherence-type-procedure.md) ground truth:
        // "10-MISE EN SERVICE DU COMPRESSEUR" (tasks 49-88) has 3 TYPE runs -- REL 49-72, MAD 73-78,
        // REL 79-88 -- the only such section across all 3 real fixtures (calibration check in the
        // ticket's own preamble).
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Errors.Should().ContainSingle();
        var error = result.Errors[0];
        error.Code.Should().Be(ExtractionErrorCode.TacheMultipleTypeMismatch);
        error.Sheet.Should().Be(Sheet);
        error.BlockIdentifier.Should().Be("10-MISE EN SERVICE DU COMPRESSEUR (tâches 73-78)");
        error.Message.Should().Be(
            "Incohérence de TYPE détectée dans la tâche multiple \"10-MISE EN SERVICE DU COMPRESSEUR\" : " +
            "tâches 73–78 en TM_PROC_MAD, encadrées par des tâches en TM_PROC_REL — vérifier une possible erreur de saisie.");

        // Non-régression : l'anomalie ne bloque rien, les tâches 73-78 sont extraites normalement.
        result.Equipement.Should().NotBeNull();
        for (var ordre = 73; ordre <= 78; ordre++)
        {
            result.TachesMultiples.Should().Contain(t => t.Ordre == ordre && t.TypeTacheMultipleCode == "TM_PROC_MAD");
        }
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

    [Theory]
    [InlineData("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx")]
    [InlineData("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx")]
    public void Extract_HomogeneousFixtures_ProduceNoTypeIncoherenceWarning(string fileName)
    {
        // Explicit false-positive guard-rail (Lot 032's own calibration note): every section in these
        // 2 fixtures is perfectly homogeneous, unlike C7401's single anomalous section above.
        var result = ExtractFromFixture(fileName);

        result.Errors.Should().NotContain(e => e.Code == ExtractionErrorCode.TacheMultipleTypeMismatch);
    }

    private ImportResult ExtractFromFixture(string fileName)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _sut.Extract(workbookReader, CreateSheetRule(), ReperePrefix, EquipementTypeElementNom, DefaultTableaux);
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
