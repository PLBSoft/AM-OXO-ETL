using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo;

// Covers PLATINES' shape specifically (Step=8, 7 unconditional Colonnes) -- also exercised by
// ORIFICES CAPACITES (Lot C4) reusing the same service with a different SheetExtractionRule, see
// OrificesCapacitesExtractionServiceIntegrationTests / the CLAUDE.md note on why this is shared.
public class UnconditionalIsolementSheetExtractionServiceTests
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

    private static SheetExtractionRule CreateSheetRule(
        IReadOnlyList<FieldPresencePointRule>? fieldPresencePointRules = null,
        BlockFieldDefinition? couleurEtiquetteCell = null) => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 17, 8, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
        ]),
        [],
        UnconditionalColonneNames, [], [],
        fieldPresencePointRules: fieldPresencePointRules,
        couleurEtiquetteCell: couleurEtiquetteCell);

    private static Mock<IWorkbookReader> CreateWorkbookReader(IReadOnlyDictionary<string, string?> cells)
    {
        var mock = new Mock<IWorkbookReader>();
        mock.Setup(r => r.ReadCellValue(Sheet, It.IsAny<string>()))
            .Returns((string _, string range) => cells.GetValueOrDefault(range));
        return mock;
    }

    [Fact]
    public void Extract_WithOneBlock_ReturnsIsolementWithComposedRepere()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:U6"] = "C7401",
            ["B17:E18"] = "PT1",
            ["H16:V17"] = "Aspiration 1er étage",
            ["B20:E22"] = "PLATINE",
            ["B25:E26"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Repere = "C7401-PT1",
            Designation = "Aspiration 1er étage",
            TypeElementNom = "PLATINE",
            PositionALaPose = ""
        });
    }

    [Fact]
    public void Extract_CreatesAllSevenUnconditionalPointsForEveryIsolement()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:U6"] = "C7401",
            ["B17:E18"] = "PT1",
            ["H16:V17"] = "Aspiration",
            ["B20:E22"] = "PLATINE",
            ["B25:E26"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Points.Should().HaveCount(7);
        result.Points.Should().OnlyContain(p => p.ParentRepere == "C7401-PT1");
        result.Points.Select(p => p.ColonneNom).Should().BeEquivalentTo(UnconditionalColonneNames);
    }

    [Fact]
    public void Extract_StopsAtFirstBlankIdentification_WithoutReadingBeyond()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:U6"] = "C7401",
            ["B17:E18"] = "PT1",
            ["H16:V17"] = "Aspiration",
            ["B20:E22"] = "PLATINE",
            ["B25:E26"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        _sut.Extract(workbookReader.Object, CreateSheetRule());

        workbookReader.Verify(r => r.ReadCellValue(Sheet, "H24:V25"), Times.Never);
    }

    [Fact]
    public void Extract_WithMultipleBlocks_ReadsAllUntilBlankIdentification()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:U6"] = "C7401",
            ["B17:E18"] = "PT1",
            ["H16:V17"] = "Aspiration",
            ["B20:E22"] = "PLATINE",
            ["B25:E26"] = "TP1",
            ["H24:V25"] = "Refoulement",
            ["B28:E30"] = "TAMPON PLEIN",
            ["B33:E34"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().HaveCount(2);
        result.Isolements[1].Repere.Should().Be("C7401-TP1");
        result.Isolements[1].TypeElementNom.Should().Be("TAMPON PLEIN");
        result.Points.Should().HaveCount(14);
    }

    [Fact]
    public void Extract_WithBlankTypeElement_ReportsRequiredFieldMissingAndSkipsBlock()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:U6"] = "C7401",
            ["B17:E18"] = "PT1",
            ["H16:V17"] = "Aspiration",
            ["B20:E22"] = null,
            ["B25:E26"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().BeEmpty();
        result.Points.Should().BeEmpty();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.RequiredFieldMissing);
    }

    // PLATINES client feedback (2026-09): a Point is created only when the block's own optional cell
    // is non-blank -- exercised here with colonne names distinct from UnconditionalColonneNames above
    // (which this generic-service test file keeps unrelated to any particular sheet's real
    // configuration) so the two mechanisms' Points are never ambiguous in an assertion.
    private static readonly FieldPresencePointRule PoseeLeRule = new(
        new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "RECEPTION DEBUT MAD (FIELD PRESENCE)");
    private static readonly FieldPresencePointRule DeposeeLeRule = new(
        new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3), "RECEPTION DEBUT REL (FIELD PRESENCE)");

    private static Dictionary<string, string?> CreateOneBlockCells() => new()
    {
        ["K6:U6"] = "C7401",
        ["B17:E18"] = "PT1",
        ["H16:V17"] = "Aspiration",
        ["B20:E22"] = "PLATINE",
        ["B25:E26"] = null
    };

    [Fact]
    public void Extract_WithFieldPresencePointRuleAndNonBlankCell_CreatesThePoint()
    {
        var cells = CreateOneBlockCells();
        cells["H19:N19"] = "12/03/2026";
        var workbookReader = CreateWorkbookReader(cells);
        var sheetRule = CreateSheetRule([PoseeLeRule]);

        var result = _sut.Extract(workbookReader.Object, sheetRule);

        result.Points.Should().Contain(p => p.ColonneNom == "RECEPTION DEBUT MAD (FIELD PRESENCE)" && p.ParentRepere == "C7401-PT1");
    }

    [Fact]
    public void Extract_WithFieldPresencePointRuleAndBlankCell_DoesNotCreateThePoint()
    {
        var cells = CreateOneBlockCells();
        var workbookReader = CreateWorkbookReader(cells);
        var sheetRule = CreateSheetRule([PoseeLeRule]);

        var result = _sut.Extract(workbookReader.Object, sheetRule);

        result.Points.Should().NotContain(p => p.ColonneNom == "RECEPTION DEBUT MAD (FIELD PRESENCE)");
    }

    [Fact]
    public void Extract_WithFieldPresencePointRuleAndWhitespaceOnlyCell_DoesNotCreateThePoint()
    {
        var cells = CreateOneBlockCells();
        cells["H19:N19"] = "   ";
        var workbookReader = CreateWorkbookReader(cells);
        var sheetRule = CreateSheetRule([PoseeLeRule]);

        var result = _sut.Extract(workbookReader.Object, sheetRule);

        result.Points.Should().NotContain(p => p.ColonneNom == "RECEPTION DEBUT MAD (FIELD PRESENCE)");
    }

    [Fact]
    public void Extract_WithMultipleFieldPresencePointRules_EvaluatesEachIndependently()
    {
        var cells = CreateOneBlockCells();
        cells["H19:N19"] = "12/03/2026";
        // H20:N20 (DeposeeLe) left blank/absent from the dictionary on purpose.
        var workbookReader = CreateWorkbookReader(cells);
        var sheetRule = CreateSheetRule([PoseeLeRule, DeposeeLeRule]);

        var result = _sut.Extract(workbookReader.Object, sheetRule);

        result.Points.Should().Contain(p => p.ColonneNom == "RECEPTION DEBUT MAD (FIELD PRESENCE)");
        result.Points.Should().NotContain(p => p.ColonneNom == "RECEPTION DEBUT REL (FIELD PRESENCE)");
    }

    [Fact]
    public void Extract_WithNoFieldPresencePointRules_NeverReadsAnyExtraCellsBeyondTheDeclaredFields()
    {
        var cells = CreateOneBlockCells();
        var workbookReader = CreateWorkbookReader(cells);
        var sheetRule = CreateSheetRule();

        _sut.Extract(workbookReader.Object, sheetRule);

        workbookReader.Verify(r => r.ReadCellValue(Sheet, "H19:N19"), Times.Never);
        workbookReader.Verify(r => r.ReadCellValue(Sheet, "H20:N20"), Times.Never);
    }

    // Lot 068: PLATINES "couleur d'étiquette" -- H:N, block offset +1 (H18:N18 for the first block,
    // FirstBlockStartRow=17), read straight into IsolementPivot.CouleurEtiquette, unrelated to any
    // Point/Colonne.
    private static readonly BlockFieldDefinition CouleurEtiquetteCell = new("CouleurEtiquette", "H:N", 1, 1);

    [Fact]
    public void Extract_WithCouleurEtiquetteCellConfiguredAndNonBlankValue_SetsCouleurEtiquetteOnPivot()
    {
        var cells = CreateOneBlockCells();
        cells["H18:N18"] = "ROUGE";
        var workbookReader = CreateWorkbookReader(cells);
        var sheetRule = CreateSheetRule(couleurEtiquetteCell: CouleurEtiquetteCell);

        var result = _sut.Extract(workbookReader.Object, sheetRule);

        result.Isolements.Should().ContainSingle().Which.CouleurEtiquette.Should().Be("ROUGE");
    }

    [Fact]
    public void Extract_WithCouleurEtiquetteCellConfiguredAndBlankValue_SetsCouleurEtiquetteToEmptyString()
    {
        var cells = CreateOneBlockCells();
        var workbookReader = CreateWorkbookReader(cells);
        var sheetRule = CreateSheetRule(couleurEtiquetteCell: CouleurEtiquetteCell);

        var result = _sut.Extract(workbookReader.Object, sheetRule);

        result.Isolements.Should().ContainSingle().Which.CouleurEtiquette.Should().BeEmpty();
    }

    [Fact]
    public void Extract_WithNoCouleurEtiquetteCellConfigured_SetsCouleurEtiquetteToEmptyString_AndNeverReadsThatCell()
    {
        var cells = CreateOneBlockCells();
        cells["H18:N18"] = "ROUGE";
        var workbookReader = CreateWorkbookReader(cells);
        var sheetRule = CreateSheetRule();

        var result = _sut.Extract(workbookReader.Object, sheetRule);

        result.Isolements.Should().ContainSingle().Which.CouleurEtiquette.Should().BeEmpty();
        workbookReader.Verify(r => r.ReadCellValue(Sheet, "H18:N18"), Times.Never);
    }

    [Fact]
    public void Extract_WithDifferentCouleurEtiquetteCellsAcrossTwoProfiles_EachRestitutesItsOwnValue()
    {
        var firstProfileCells = CreateOneBlockCells();
        firstProfileCells["H18:N18"] = "ROUGE";
        var firstResult = _sut.Extract(
            CreateWorkbookReader(firstProfileCells).Object,
            CreateSheetRule(couleurEtiquetteCell: CouleurEtiquetteCell));

        var otherCell = new BlockFieldDefinition("CouleurEtiquette", "B:E", 5, 5);
        var secondProfileCells = CreateOneBlockCells();
        secondProfileCells["B22:E22"] = "BLEUE";
        var secondResult = _sut.Extract(
            CreateWorkbookReader(secondProfileCells).Object,
            CreateSheetRule(couleurEtiquetteCell: otherCell));

        firstResult.Isolements.Should().ContainSingle().Which.CouleurEtiquette.Should().Be("ROUGE");
        secondResult.Isolements.Should().ContainSingle().Which.CouleurEtiquette.Should().Be("BLEUE");
    }
}
