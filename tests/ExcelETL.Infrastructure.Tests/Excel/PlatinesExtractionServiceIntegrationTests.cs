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

// Runs UnconditionalIsolementSheetExtractionService (Application, Lot C3) against the real
// ClosedXmlWorkbookReader and the 3 real client fixtures, configured for PLATINES specifically.
public class PlatinesExtractionServiceIntegrationTests
{
    private const string Sheet = "PLATINES";

    private static readonly string[] UnconditionalColonneNames =
    [
        "POSE ÉTIQUETTES",
        "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
        "CONTRÔLE ETANCHÉITÉS",
        "RECEPTION DEBUT MAD",
        "RÉCEPTION PLATINES/TAMPONS PLEINS",
        "RECEPTION DEBUT REL",
        "PLATINES / TAMPONS PLEINS"
    ];

    private readonly UnconditionalIsolementSheetExtractionService _sut = new(
        new RepeatingBlockReader(), new TextTransformEvaluator(),
        NullLogger<UnconditionalIsolementSheetExtractionService>.Instance);

    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 17, 8, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
        ]),
        [],
        UnconditionalColonneNames, [], []);

    [Fact]
    public void Extract_C7401Fixture_ReturnsAllPlatinesWithNoErrors()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Errors.Should().BeEmpty();
        result.Isolements.Should().HaveCount(15);
        result.Isolements.Should().OnlyContain(i => i.TypeElementNom == "PLATINE");
        result.Points.Should().HaveCount(15 * 7);
    }

    [Fact]
    public void Extract_D8570Fixture_ReturnsPlatinesAndTamponPleinWithNoErrors()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        result.Errors.Should().BeEmpty();
        result.Isolements.Should().HaveCount(21);
        result.Isolements.Should().OnlyContain(i => i.TypeElementNom == "PLATINE" || i.TypeElementNom == "TAMPON PLEIN");
        result.Points.Should().HaveCount(21 * 7);
    }

    [Fact]
    public void Extract_G6306BFixture_ReturnsPlatinesAndTamponPleinWithNoErrors()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        result.Errors.Should().BeEmpty();
        result.Isolements.Should().HaveCount(5);
        result.Isolements.Should().OnlyContain(i => i.TypeElementNom == "PLATINE" || i.TypeElementNom == "TAMPON PLEIN");
        result.Points.Should().HaveCount(5 * 7);
    }

    // PLATINES client clarification (2026-09-16): "RECEPTION DEBUT MAD"/"RECEPTION DEBUT REL" are ticked
    // only when one of the block's two H value cells (POSÉE LE +2 or DÉPOSÉE LE +3 -- the row label
    // doesn't matter) holds the exact text "DEBUT MAD"/"DEBUT REL". Same 4 rules as DefaultProfileSeeder,
    // verified here against the real fixtures (not the hand-built cells
    // UnconditionalIsolementSheetExtractionServiceTests uses). Expected values below come from a direct
    // dump of every fixture's PLATINES H cells.
    private const string DebutMad = "RECEPTION DEBUT MAD";
    private const string DebutRel = "RECEPTION DEBUT REL";

    private static readonly FieldPresencePointRule[] DebutRules =
    [
        new(new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), DebutMad, "DEBUT MAD"),
        new(new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3), DebutMad, "DEBUT MAD"),
        new(new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), DebutRel, "DEBUT REL"),
        new(new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3), DebutRel, "DEBUT REL")
    ];

    private static SheetExtractionRule CreateSheetRuleWithFieldPresenceRules() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 17, 8, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
        ]),
        [],
        // The 5 Colonnes that stay unconditional (PoseEtiquettes and friends) -- unaffected by this
        // feature, kept here only so the total Point count assertions below are meaningful.
        [
            "POSE ÉTIQUETTES",
            "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
            "CONTRÔLE ETANCHÉITÉS",
            "RÉCEPTION PLATINES/TAMPONS PLEINS",
            "PLATINES / TAMPONS PLEINS"
        ],
        [], [],
        fieldPresencePointRules: DebutRules);

    private static IReadOnlyList<string> DebutColonnesOf(IsolementSheetExtractionResult result, string identification) =>
        result.Points
            .Where(p => p.ParentRepere == result.Isolements
                .Single(i => i.Repere.EndsWith("-" + identification, StringComparison.Ordinal)).Repere)
            .Select(p => p.ColonneNom)
            .Where(c => c is DebutMad or DebutRel)
            .Order(StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void Extract_C7401Fixture_PT15AGetsOnlyDebutMad_PT15BGetsOnlyDebutRel()
    {
        // PT15A: H123 "DEBUT MAD" / H124 "FIN MAD"; PT15B: H131 "DEBUT REL" / H132 "FIN REL". Before the
        // client clarification both blocks got both DEBUT Points (any filled cell counted).
        var result = ExtractFromFixtureWithFieldPresenceRules("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Errors.Should().BeEmpty();
        result.Isolements.Should().HaveCount(15);
        DebutColonnesOf(result, "PT15A").Should().Equal(DebutMad);
        DebutColonnesOf(result, "PT15B").Should().Equal(DebutRel);
        result.Points.Should().HaveCount(15 * 5 + 2);
    }

    [Fact]
    public void Extract_C8503Fixture_DebutValuesInEitherRowTickBothColonnes_FinValuesTickNone()
    {
        // The client's own screenshot file: TP3/TP6/TP9 carry "DEBUT REL" in POSÉE LE and "DEBUT MAD" in
        // DÉPOSÉE LE; TP14 carries "FIN REL"/"FIN MAD".
        var result = ExtractFromFixtureWithFieldPresenceRules("Dossier de MaD IDL -  C8503 PORTE FILTRE.xlsx");

        foreach (var identification in new[] { "TP3", "TP6", "TP9" })
        {
            DebutColonnesOf(result, identification).Should().Equal(DebutMad, DebutRel);
        }

        DebutColonnesOf(result, "TP14").Should().BeEmpty();
    }

    [Fact]
    public void Extract_E6431AFixture_DebutMadInPoseeLeOrDeposeeLe_TicksDebutMadOnlyOnce()
    {
        // P1-P4: "DEBUT MAD" in POSÉE LE; TP1-TP4: "DEBUT MAD" in DÉPOSÉE LE (with "FIN MAD" in POSÉE LE).
        var result = ExtractFromFixtureWithFieldPresenceRules("Dossier de MaD IDL -  E6431A Dépose cellule.xlsx");

        foreach (var identification in new[] { "P1", "P2", "P3", "P4", "TP1", "TP2", "TP3", "TP4" })
        {
            DebutColonnesOf(result, identification).Should().Equal(DebutMad);
        }
    }

    [Theory]
    [InlineData("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx", 21)]
    [InlineData("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx", 5)]
    [InlineData("Dossier de MaD IDL -  G4010A.xlsx", 4)]
    public void Extract_FixturesWithNoDataInPoseeLeOrDeposeeLe_NeverCreateEitherPoint(
        string fixtureFileName, int expectedIsolementCount)
    {
        var result = ExtractFromFixtureWithFieldPresenceRules(fixtureFileName);

        result.Errors.Should().BeEmpty();
        result.Isolements.Should().HaveCount(expectedIsolementCount);
        result.Points.Should().NotContain(p => p.ColonneNom == DebutMad || p.ColonneNom == DebutRel);
        result.Points.Should().HaveCount(expectedIsolementCount * 5);
    }

    // Lot 068 (couleur d'étiquette, client remark) -- verified against the real fixtures, not
    // hand-built cells, per the ticket's own explicit requirement (68.7).
    private static SheetExtractionRule CreateSheetRuleWithCouleurEtiquetteCell() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 17, 8, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
        ]),
        [], UnconditionalColonneNames, [], [],
        couleurEtiquetteCell: new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1));

    [Fact]
    public void Extract_C7401Fixture_WithCouleurEtiquetteCell_EveryPlatineIsRouge()
    {
        var result = ExtractFromFixtureWithCouleurEtiquetteCell("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Isolements.Should().HaveCount(15);
        result.Isolements.Should().OnlyContain(i => i.CouleurEtiquette == "ROUGE");
    }

    [Fact]
    public void Extract_D8570Fixture_WithCouleurEtiquetteCell_MatchesRealFixtureColorsPerBlock()
    {
        var result = ExtractFromFixtureWithCouleurEtiquetteCell("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        result.Isolements.Should().HaveCount(21);
        // Confirmed by direct inspection of the real fixture (PLATINES, column H, rows 18..178):
        // the first 13 blocks are ROUGE, the next 8 are BLEUE -- no block is blank on this fixture.
        result.Isolements.Take(13).Should().OnlyContain(i => i.CouleurEtiquette == "ROUGE");
        result.Isolements.Skip(13).Should().OnlyContain(i => i.CouleurEtiquette == "BLEUE");
    }

    [Fact]
    public void Extract_G6306BFixture_WithCouleurEtiquetteCell_FourthBlockIsJaune()
    {
        var result = ExtractFromFixtureWithCouleurEtiquetteCell("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        result.Isolements.Should().HaveCount(5);
        // Confirmed by direct inspection: real free text, not a closed ROUGE/BLEUE set -- block 5
        // (0-indexed 4, row 50) is JAUNE, every other block is ROUGE.
        result.Isolements.Where((_, index) => index != 4).Should().OnlyContain(i => i.CouleurEtiquette == "ROUGE");
        result.Isolements[4].CouleurEtiquette.Should().Be("JAUNE");
    }

    [Fact]
    public void Extract_WithoutCouleurEtiquetteCellConfigured_LeavesCouleurEtiquetteEmpty()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Isolements.Should().OnlyContain(i => i.CouleurEtiquette == "");
    }

    private IsolementSheetExtractionResult ExtractFromFixtureWithCouleurEtiquetteCell(string fileName)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _sut.Extract(workbookReader, CreateSheetRuleWithCouleurEtiquetteCell());
    }

    private IsolementSheetExtractionResult ExtractFromFixtureWithFieldPresenceRules(string fileName)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _sut.Extract(workbookReader, CreateSheetRuleWithFieldPresenceRules());
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
