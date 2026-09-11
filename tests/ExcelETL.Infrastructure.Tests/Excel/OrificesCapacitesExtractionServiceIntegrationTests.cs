using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

// ORIFICES CAPACITES (Lot C4) reuses UnconditionalIsolementSheetExtractionService as-is -- its cell
// ranges/step/offsets are byte-identical to PLATINES per the spec, only the sheet name and Colonne
// list differ, both already parameterized via SheetExtractionRule. No new Application-layer unit
// tests are added here: the shared service's mechanics are already covered by
// UnconditionalIsolementSheetExtractionServiceTests (Application.Tests); this integration test is
// what actually exercises the ORIFICES CAPACITES-specific configuration end-to-end.
public class OrificesCapacitesExtractionServiceIntegrationTests
{
    private const string Sheet = "ORIFICES CAPACITES";

    private static readonly string[] UnconditionalColonneNames =
    [
        "POSE ÉTIQUETTES",
        "RÉCEPTION PLATINES/TAMPONS PLEINS",
        "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
        "CONTRÔLE ETANCHÉITÉS"
    ];

    private readonly UnconditionalIsolementSheetExtractionService _sut = new(
        new RepeatingBlockReader(), new TextTransformEvaluator(),
        NullLogger<UnconditionalIsolementSheetExtractionService>.Instance);

    private static SheetExtractionRule CreateSheetRule(BlockFieldDefinition? couleurEtiquetteCell = null) => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 17, 8, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
        ]),
        [],
        UnconditionalColonneNames, [], [],
        couleurEtiquetteCell: couleurEtiquetteCell);

    [Fact]
    public void Extract_C7401Fixture_ReturnsNoIsolements()
    {
        // Confirmed against the real file: this dossier's ORIFICES CAPACITES sheet is present but
        // empty (no header, no rows) -- a legitimate "sheet not used for this dossier" case, not an
        // error. The generic engine already handles a blank first Identification cell gracefully.
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Isolements.Should().BeEmpty();
        result.Points.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_D8570Fixture_ReturnsAllTrouDHommeWithNoErrors()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        result.Errors.Should().BeEmpty();
        result.Isolements.Should().HaveCount(5);
        result.Isolements.Should().OnlyContain(i => i.TypeElementNom == "TROU D'HOMME");
        result.Points.Should().HaveCount(5 * 4);
    }

    [Fact]
    public void Extract_G6306BFixture_ReturnsAllTrouDHommeWithNoErrors()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        result.Errors.Should().BeEmpty();
        result.Isolements.Should().HaveCount(2);
        result.Isolements.Should().OnlyContain(i => i.TypeElementNom == "TROU D'HOMME");
        result.Points.Should().HaveCount(2 * 4);
    }

    // Client feedback (2026-09): ORIFICES CAPACITES shares PLATINES' exact "couleur d'étiquette" cell
    // (H:N, block offset +1) -- client-confirmed to implement as specified, even though direct
    // inspection of every real fixture on disk (C7401/D8570/G6306B/G4010A/C8503) shows that cell
    // always holds the literal, static text "DATE" (the repeated sub-header of a per-block "ETAT et
    // TEST / DATE / NOM / SIGNATURE" mini-table, not real user-entered color data) -- no
    // "ÉTIQUETTE"/"COULEUR" label exists anywhere on this sheet in any fixture on disk today. The
    // client's own source template for this sheet hasn't been updated to carry a real color yet; this
    // wiring is accepted as forward-looking (a future client file may populate it for real), same
    // "accepted as-is, no special handling" precedent as PLATINES' own DEBUT/FIN block-split anomaly.
    // The mechanism itself (read whatever the configured cell holds) is already fully proven correct
    // against real PLATINES data in PlatinesExtractionServiceIntegrationTests -- this test only
    // documents the current, known-stale literal content for ORIFICES CAPACITES specifically.
    [Fact]
    public void Extract_D8570Fixture_WithCouleurEtiquetteCell_ReadsTheConfiguredCell_CurrentlyTheStaticDateHeader()
    {
        var rule = CreateSheetRule(couleurEtiquetteCell: new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1));
        using var stream = File.OpenRead(FixturePath("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx"));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);

        var result = _sut.Extract(workbookReader, rule);

        result.Isolements.Should().HaveCount(5);
        result.Isolements.Should().OnlyContain(i => i.CouleurEtiquette == "DATE");
    }

    private IsolementSheetExtractionResult ExtractFromFixture(string fileName)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _sut.Extract(workbookReader, CreateSheetRule());
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
