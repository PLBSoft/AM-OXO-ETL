using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo.AutresJointsTouches;

public class AutresJointsTouchesExtractionServiceTests
{
    private const string Sheet = "AUTRES JOINTS TOUCHES";
    private const string PoseEtiquettesColonneName = "POSE ÉTIQUETTES";
    private const string ReperePrefix = "MAD-OXO-";

    private static readonly string[] UnconditionalColonneNames =
    [
        "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
        "CONTRÔLE ETANCHÉITÉS"
    ];

    private readonly AutresJointsTouchesExtractionService _sut =
        new(new RepeatingBlockReader(), new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(),
            new HeaderRuleResolver(new TextTransformEvaluator()), NullLogger<AutresJointsTouchesExtractionService>.Instance);

    // Lot 047: the "repereEcho" header rule (N6) -- transcribed from the coordinate previously
    // hardcoded in AutresJointsTouchesExtractionService -- so this test's Mock<IWorkbookReader> "N6"
    // cell key stays exactly as before the lot.
    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 17, 7, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "F:Y", -1, 0),
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
        ]),
        [new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.NotEquals, "TUBING", PoseEtiquettesColonneName)],
        UnconditionalColonneNames,
        [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell(Sheet, "N6"))],
        []);

    private static Mock<IWorkbookReader> CreateWorkbookReader(IReadOnlyDictionary<string, string?> cells)
    {
        var mock = new Mock<IWorkbookReader>();
        mock.Setup(r => r.ReadCellValue(Sheet, It.IsAny<string>()))
            .Returns((string _, string range) => cells.GetValueOrDefault(range));
        return mock;
    }

    private static Dictionary<string, string?> BaseBlockCells(string typeElement) => new()
    {
        ["N6"] = "D8570",
        ["B17:E18"] = "JT1",
        ["F16:Y17"] = "Piquage supérieur",
        ["B20:E21"] = typeElement,
        ["B24:E25"] = null
    };

    [Fact]
    public void Extract_WithOneBlock_ReturnsIsolementWithComposedRepere()
    {
        var cells = BaseBlockCells("TUYAUTERIE");
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix);

        result.Isolements.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Repere = "D8570-JT1",
            Designation = "Piquage supérieur",
            TypeElementNom = "TUYAUTERIE",
            PositionALaPose = ""
        });
    }

    [Fact]
    public void Extract_CreatesUnconditionalPointsForEveryIsolement()
    {
        var cells = BaseBlockCells("TUYAUTERIE");
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix);

        result.Points.Should().Contain(
        [
            new PointPivot("RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "D8570-JT1"),
            new PointPivot("CONTRÔLE ETANCHÉITÉS", "D8570-JT1")
        ]);
    }

    [Fact]
    public void Extract_WithTuyauterieType_CreatesPoseEtiquettesPoint()
    {
        var cells = BaseBlockCells("TUYAUTERIE");
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix);

        result.Points.Should().Contain(new PointPivot(PoseEtiquettesColonneName, "D8570-JT1"));
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_WithTubingType_DoesNotCreatePoseEtiquettesPointAndWarns()
    {
        var cells = BaseBlockCells("TUBING");
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix);

        result.Points.Should().NotContain(p => p.ColonneNom == PoseEtiquettesColonneName);
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.NoConditionalPointCreated);
    }

    [Fact]
    public void Extract_ReadsRepereEchoFromWhicheverCoordinateTheProfileDeclares_NotAHardcodedConstant()
    {
        // Lot 047, 47.5 anti-hardcoding guard-rail: a profile whose "repereEcho" HeaderFieldRule
        // points at a different cell than DefaultProfileSeeder's N6 must be honored.
        var sheetRule = new SheetExtractionRule(
            Sheet,
            new RepeatingBlockLocator(Sheet, 17, 7, IsolementFieldNames.Identification,
            [
                new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                new BlockFieldDefinition(IsolementFieldNames.Designation, "F:Y", -1, 0),
                new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
            ]),
            [new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.NotEquals, "TUBING", PoseEtiquettesColonneName)],
            UnconditionalColonneNames,
            [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell(Sheet, "Z9"))],
            []);
        var cells = BaseBlockCells("TUYAUTERIE");
        cells.Remove("N6");
        cells["Z9"] = "OTHERREPERE";
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, sheetRule, ReperePrefix);

        result.Isolements.Should().ContainSingle().Which.Repere.Should().Be("OTHERREPERE-JT1");
    }

    [Fact]
    public void Extract_StopsAtFirstBlankIdentification_WithoutReadingBeyond()
    {
        var cells = BaseBlockCells("TUYAUTERIE");
        var workbookReader = CreateWorkbookReader(cells);

        _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix);

        workbookReader.Verify(r => r.ReadCellValue(Sheet, "F23:Y24"), Times.Never);
    }

    [Fact]
    public void Extract_WithBlankTypeElement_ReportsRequiredFieldMissingAndSkipsBlock()
    {
        var cells = BaseBlockCells("TUYAUTERIE");
        cells["B20:E21"] = null;
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule(), ReperePrefix);

        result.Isolements.Should().BeEmpty();
        result.Points.Should().BeEmpty();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.RequiredFieldMissing);
    }
}
