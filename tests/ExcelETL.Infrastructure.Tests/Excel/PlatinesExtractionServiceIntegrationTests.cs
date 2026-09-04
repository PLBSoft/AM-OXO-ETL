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

    // PLATINES client feedback (2026-09): "RECEPTION DEBUT MAD"/"RECEPTION DEBUT REL" become
    // field-presence-driven instead of unconditional -- verified here against the real fixtures
    // (not the hand-built cells UnconditionalIsolementSheetExtractionServiceTests uses), including
    // C7401's PT15A/PT15B block-split anomaly (spec §3, "jugé non fiable") and G4010A, the file
    // behind the client's own screenshot.
    private static readonly FieldPresencePointRule PoseeLeRule = new(
        new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "RECEPTION DEBUT MAD");
    private static readonly FieldPresencePointRule DeposeeLeRule = new(
        new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3), "RECEPTION DEBUT REL");

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
        fieldPresencePointRules: [PoseeLeRule, DeposeeLeRule]);

    [Fact]
    public void Extract_C7401Fixture_WithFieldPresenceRules_OnlyPT15AAndPT15BGetBothPoints()
    {
        var result = ExtractFromFixtureWithFieldPresenceRules("Dossier.de.MaD.IDL.-.C7401.xlsx");

        result.Errors.Should().BeEmpty();
        result.Isolements.Should().HaveCount(15);

        var pt15A = result.Isolements.Single(i => i.Repere.EndsWith("-PT15A", StringComparison.Ordinal));
        var pt15B = result.Isolements.Single(i => i.Repere.EndsWith("-PT15B", StringComparison.Ordinal));
        result.Points.Should().Contain(p => p.ParentRepere == pt15A.Repere && p.ColonneNom == "RECEPTION DEBUT MAD");
        result.Points.Should().Contain(p => p.ParentRepere == pt15A.Repere && p.ColonneNom == "RECEPTION DEBUT REL");
        result.Points.Should().Contain(p => p.ParentRepere == pt15B.Repere && p.ColonneNom == "RECEPTION DEBUT MAD");
        result.Points.Should().Contain(p => p.ParentRepere == pt15B.Repere && p.ColonneNom == "RECEPTION DEBUT REL");

        // Every other block (13 of the 15 platines) has both source cells blank -- neither Point.
        var otherIsolements = result.Isolements.Where(i => i != pt15A && i != pt15B);
        otherIsolements.Should().OnlyContain(i =>
            !result.Points.Any(p => p.ParentRepere == i.Repere &&
                (p.ColonneNom == "RECEPTION DEBUT MAD" || p.ColonneNom == "RECEPTION DEBUT REL")));

        result.Points.Should().HaveCount(15 * 5 + 4);
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
        result.Points.Should().NotContain(p =>
            p.ColonneNom == "RECEPTION DEBUT MAD" || p.ColonneNom == "RECEPTION DEBUT REL");
        result.Points.Should().HaveCount(expectedIsolementCount * 5);
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
