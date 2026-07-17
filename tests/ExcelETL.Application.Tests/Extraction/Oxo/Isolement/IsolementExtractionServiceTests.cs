using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo.Isolement;

public class IsolementExtractionServiceTests
{
    private const string Sheet = "ISOLEMENT";
    private const string ZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)";

    private readonly IsolementExtractionService _sut =
        new(new TextTransformEvaluator(), new ConditionalPointRuleEvaluator());

    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 19, 7, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "H:U", -1, 0),
            new BlockFieldDefinition(IsolementFieldNames.PositionALaPose, "H:O", 1, 2),
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
        ]),
        [new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", ZeroEnergieColonneName)],
        ["PROLOCK VANNES", "DEPROLOCK VANNES"]);

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
            ["K6:T6"] = "C7401",
            ["B19:E20"] = "V1",
            ["H18:U19"] = "Aspiration 1er étage",
            ["H20:O21"] = "FERMÉE",
            ["B22:E23"] = "PROLOCK",
            ["B26:E27"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Repere = "C7401-V1",
            Designation = "Aspiration 1er étage",
            TypeElementNom = "PROLOCK",
            PositionALaPose = "FERMÉE"
        });
    }

    [Fact]
    public void Extract_StopsAtFirstBlankIdentification_WithoutReadingBeyond()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:T6"] = "C7401",
            ["B19:E20"] = "V1",
            ["H18:U19"] = "Aspiration",
            ["H20:O21"] = "FERMÉE",
            ["B22:E23"] = "PROLOCK",
            ["B26:E27"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        _sut.Extract(workbookReader.Object, CreateSheetRule());

        workbookReader.Verify(r => r.ReadCellValue(Sheet, "H25:U26"), Times.Never);
    }

    [Fact]
    public void Extract_CreatesUnconditionalPointsForEveryIsolement()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:T6"] = "C7401",
            ["B19:E20"] = "V1",
            ["H18:U19"] = "Aspiration",
            ["H20:O21"] = "FERMÉE",
            ["B22:E23"] = "PROLOCK",
            ["B26:E27"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Points.Should().Contain(
        [
            new PointPivot("PROLOCK VANNES", "C7401-V1"),
            new PointPivot("DEPROLOCK VANNES", "C7401-V1")
        ]);
    }

    [Fact]
    public void Extract_WithZeroEnergieType_CreatesConditionalPoint()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:T6"] = "C7401",
            ["B19:E20"] = "V1",
            ["H18:U19"] = "Aspiration",
            ["H20:O21"] = "FERMÉE",
            ["B22:E23"] = "ZERO ENERGIE",
            ["B26:E27"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Points.Should().Contain(new PointPivot(ZeroEnergieColonneName, "C7401-V1"));
    }

    [Fact]
    public void Extract_WithVanneType_ExtractsIsolementNormallyWithBlankDesignationAndWarns()
    {
        // Real D8570 fixture, row 117: Identification "V4", TypeElement "VANNE" (confirmed absent
        // from the OXO referential), Designation blank. Must still be extracted (not rejected).
        var cells = new Dictionary<string, string?>
        {
            ["K6:T6"] = "D8570",
            ["B19:E20"] = "V4",
            ["H18:U19"] = null,
            ["H20:O21"] = "FERMÉE",
            ["B22:E23"] = "VANNE",
            ["B26:E27"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().ContainSingle().Which.Designation.Should().BeEmpty();
        // "PROLOCK VANNES"/"DEPROLOCK VANNES" are unconditional -- created regardless of TypeElement.
        result.Points.Should().Contain(
        [
            new PointPivot("PROLOCK VANNES", "D8570-V4"),
            new PointPivot("DEPROLOCK VANNES", "D8570-V4")
        ]);
        result.Points.Should().NotContain(p => p.ColonneNom == ZeroEnergieColonneName);
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.UnrecognizedTypeElement);
    }

    [Fact]
    public void Extract_WithBlankTypeElement_ReportsRequiredFieldMissingAndSkipsBlock()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:T6"] = "C7401",
            ["B19:E20"] = "V1",
            ["H18:U19"] = "Aspiration",
            ["H20:O21"] = "FERMÉE",
            ["B22:E23"] = null,
            ["B26:E27"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().BeEmpty();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public void Extract_WithBlankPositionALaPose_ReportsRequiredFieldMissingAndSkipsBlock()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:T6"] = "C7401",
            ["B19:E20"] = "V1",
            ["H18:U19"] = "Aspiration",
            ["H20:O21"] = null,
            ["B22:E23"] = "PROLOCK",
            ["B26:E27"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().BeEmpty();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public void Extract_WithMultipleBlocks_ContinuesAfterASkippedBlock()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:T6"] = "C7401",
            ["B19:E20"] = "V1",
            ["H18:U19"] = "Aspiration",
            ["H20:O21"] = null, // skipped: blank PositionALaPose
            ["B22:E23"] = "PROLOCK",
            ["B26:E27"] = "V2",
            ["H25:U26"] = "Refoulement",
            ["H27:O28"] = "OUVERT",
            ["B29:E30"] = "PROLOCK",
            ["B33:E34"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().ContainSingle().Which.Repere.Should().Be("C7401-V2");
        // V1's block is skipped (RequiredFieldMissing); V2 survives but, being "PROLOCK" (not "ZERO
        // ENERGIE"), also gets an UnrecognizedTypeElement warning from the non-matching conditional
        // Colonne group -- the same evaluator behavior already established in Lot B4 (e.g. a TUBING
        // isolement not matching AUTRES JOINTS TOUCHES' "Pose Étiquettes" rule also warns).
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().ContainSingle(e => e.Code == ExtractionErrorCode.RequiredFieldMissing);
        result.Errors.Should().ContainSingle(e => e.Code == ExtractionErrorCode.UnrecognizedTypeElement);
    }
}
