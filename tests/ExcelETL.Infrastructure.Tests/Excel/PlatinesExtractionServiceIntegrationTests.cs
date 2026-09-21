using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Elements;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

// Runs the PLATINES settings through ElementSheetExtractionService (lot 084; before it
// UnconditionalIsolementSheetExtractionService, Lot C3) against the real
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

    private readonly ElementSheetExtractionService _sut = new(
        new RepeatingBlockReader(), new ConditionalPointRuleEvaluator(),
        new HeaderRuleResolver(), NullLogger<ElementSheetExtractionService>.Instance);

    private const string ReperePrefix = "MAD-OXO-";

    // Lot 084.6: run through the generic element engine; the repère echo is a header field (G6).
    private static readonly HeaderFieldRule[] RepereEcho =
        [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, "K6:U6")];

    private static readonly BlockFieldDefinition[] KnownFields =
    [
        new BlockFieldDefinition(ElementFieldNames.Identification, "B:E", 0, 1),
        new BlockFieldDefinition(ElementFieldNames.Designation, "H:V", -1, 0),
        new BlockFieldDefinition(ElementFieldNames.TypeElement, "B:E", 3, 5)
    ];

    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(17, 8, KnownFields),
        [],
        UnconditionalColonneNames, RepereEcho, []);

    [Fact]
    public void Extract_C7401Fixture_ReturnsAllPlatinesWithNoErrors()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Errors.Should().BeEmpty();
        result.Elements.Should().HaveCount(15);
        result.Elements.Should().OnlyContain(i => i.TypeElementNom == "PLATINE");
        result.Points.Should().HaveCount(15 * 7);
    }

    [Fact]
    public void Extract_D8570Fixture_ReturnsPlatinesAndTamponPleinWithNoErrors()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        result.Errors.Should().BeEmpty();
        result.Elements.Should().HaveCount(21);
        result.Elements.Should().OnlyContain(i => i.TypeElementNom == "PLATINE" || i.TypeElementNom == "TAMPON PLEIN");
        result.Points.Should().HaveCount(21 * 7);
    }

    [Fact]
    public void Extract_G6306BFixture_ReturnsPlatinesAndTamponPleinWithNoErrors()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        result.Errors.Should().BeEmpty();
        result.Elements.Should().HaveCount(5);
        result.Elements.Should().OnlyContain(i => i.TypeElementNom == "PLATINE" || i.TypeElementNom == "TAMPON PLEIN");
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

    // Lot 084.6: the two H value cells are optional block fields read by ordinary point rules (G2).
    private static readonly ConditionalPointRule[] DebutRules =
    [
        new("PoseeLe", ConditionOperator.Equals, "DEBUT MAD", DebutMad),
        new("DeposeeLe", ConditionOperator.Equals, "DEBUT MAD", DebutMad),
        new("PoseeLe", ConditionOperator.Equals, "DEBUT REL", DebutRel),
        new("DeposeeLe", ConditionOperator.Equals, "DEBUT REL", DebutRel)
    ];

    private static SheetExtractionRule CreateSheetRuleWithFieldPresenceRules() => new(
        Sheet,
        new RepeatingBlockLocator(17, 8,
        [
            .. KnownFields,
            new BlockFieldDefinition("PoseeLe", "H:N", 2, 2, isRequired: false),
            new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3, isRequired: false)
        ]),
        DebutRules,
        // The 5 Colonnes that stay unconditional (PoseEtiquettes and friends) -- unaffected by this
        // feature, kept here only so the total Point count assertions below are meaningful.
        [
            "POSE ÉTIQUETTES",
            "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
            "CONTRÔLE ETANCHÉITÉS",
            "RÉCEPTION PLATINES/TAMPONS PLEINS",
            "PLATINES / TAMPONS PLEINS"
        ],
        RepereEcho, []);

    private static IReadOnlyList<string> DebutColonnesOf(ElementSheetExtractionResult result, string identification) =>
        result.Points
            .Where(p => p.ParentRepere == result.Elements
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
        result.Elements.Should().HaveCount(15);
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
        result.Elements.Should().HaveCount(expectedIsolementCount);
        result.Points.Should().NotContain(p => p.ColonneNom == DebutMad || p.ColonneNom == DebutRel);
        result.Points.Should().HaveCount(expectedIsolementCount * 5);
    }

    // Lot 068 (couleur d'étiquette, client remark) -- verified against the real fixtures, not
    // hand-built cells, per the ticket's own explicit requirement (68.7).
    // Lot 084.6 (G10): the couleur cell is an optional block field with a known name.
    private static SheetExtractionRule CreateSheetRuleWithCouleurEtiquetteCell() => new(
        Sheet,
        new RepeatingBlockLocator(17, 8,
            [.. KnownFields, new BlockFieldDefinition(ElementFieldNames.CouleurEtiquette, "H:N", 1, 1, isRequired: false)]),
        [], UnconditionalColonneNames, RepereEcho, []);

    [Fact]
    public void Extract_C7401Fixture_WithCouleurEtiquetteCell_EveryPlatineIsRouge()
    {
        var result = ExtractFromFixtureWithCouleurEtiquetteCell("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Elements.Should().HaveCount(15);
        result.Elements.Should().OnlyContain(i => i.CouleurEtiquette == "ROUGE");
    }

    [Fact]
    public void Extract_D8570Fixture_WithCouleurEtiquetteCell_MatchesRealFixtureColorsPerBlock()
    {
        var result = ExtractFromFixtureWithCouleurEtiquetteCell("Dossier.de.MaD.IDL.-.D8570.chgt.plateaux.xlsx");

        result.Elements.Should().HaveCount(21);
        // Confirmed by direct inspection of the real fixture (PLATINES, column H, rows 18..178):
        // the first 13 blocks are ROUGE, the next 8 are BLEUE -- no block is blank on this fixture.
        result.Elements.Take(13).Should().OnlyContain(i => i.CouleurEtiquette == "ROUGE");
        result.Elements.Skip(13).Should().OnlyContain(i => i.CouleurEtiquette == "BLEUE");
    }

    [Fact]
    public void Extract_G6306BFixture_WithCouleurEtiquetteCell_FourthBlockIsJaune()
    {
        var result = ExtractFromFixtureWithCouleurEtiquetteCell("Dossier.de.MaD.IDL.-.G6306B.REV.xlsx");

        result.Elements.Should().HaveCount(5);
        // Confirmed by direct inspection: real free text, not a closed ROUGE/BLEUE set -- block 5
        // (0-indexed 4, row 50) is JAUNE, every other block is ROUGE.
        result.Elements.Where((_, index) => index != 4).Should().OnlyContain(i => i.CouleurEtiquette == "ROUGE");
        result.Elements[4].CouleurEtiquette.Should().Be("JAUNE");
    }

    [Fact]
    public void Extract_WithoutCouleurEtiquetteCellConfigured_LeavesCouleurEtiquetteEmpty()
    {
        var result = ExtractFromFixture("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Elements.Should().OnlyContain(i => i.CouleurEtiquette == "");
    }

    private ElementSheetExtractionResult ExtractFromFixtureWithCouleurEtiquetteCell(string fileName)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _sut.Extract(workbookReader, CreateSheetRuleWithCouleurEtiquetteCell(), ReperePrefix);
    }

    private ElementSheetExtractionResult ExtractFromFixtureWithFieldPresenceRules(string fileName)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _sut.Extract(workbookReader, CreateSheetRuleWithFieldPresenceRules(), ReperePrefix);
    }

    private ElementSheetExtractionResult ExtractFromFixture(string fileName)
    {
        using var stream = File.OpenRead(FixturePath(fileName));
        using var workbookReader = new ClosedXmlWorkbookReader(stream);
        return _sut.Extract(workbookReader, CreateSheetRule(), ReperePrefix);
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
