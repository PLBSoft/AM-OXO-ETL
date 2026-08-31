using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo.Isolement;

public class IsolementExtractionServiceTests
{
    private const string Sheet = "ISOLEMENT";
    private const string ZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)";

    private readonly IsolementExtractionService _sut =
        new(new TextTransformEvaluator(), new ConditionalPointRuleEvaluator(), NullLogger<IsolementExtractionService>.Instance);

    // Lot 063: PS941's condition is now tested against HasZeroEnergie ("true"/"false", derived from
    // the dedicated column V cell), not against TypeElement -- see IsolementExtractionService's own
    // comment on why. ZeroEnergieExpectedValue reproduces the client's current real value.
    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 19, 7, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "H:U", -1, 0),
            new BlockFieldDefinition(IsolementFieldNames.PositionALaPose, "H:O", 1, 2),
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4),
            new BlockFieldDefinition(IsolementFieldNames.HasZeroEnergie, "V", -1, 0)
        ]),
        [new ConditionalPointRule(IsolementFieldNames.HasZeroEnergie, ConditionOperator.Equals, "true", ZeroEnergieColonneName)],
        ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], [], zeroEnergieExpectedValue: "ZERO ENERGIE");

    // A profile predating this lot -- no HasZeroEnergie field configured at all, no
    // ZeroEnergieExpectedValue -- must keep behaving exactly as it did before this lot.
    private static SheetExtractionRule CreateSheetRuleWithoutZeroEnergieField() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 19, 7, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "H:U", -1, 0),
            new BlockFieldDefinition(IsolementFieldNames.PositionALaPose, "H:O", 1, 2),
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
        ]),
        [new ConditionalPointRule(IsolementFieldNames.HasZeroEnergie, ConditionOperator.Equals, "true", ZeroEnergieColonneName)],
        ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], []);

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
    public void Extract_WithZeroEnergieCellMatchingExpectedValue_CreatesConditionalPointAndSetsHasZeroEnergie()
    {
        // Real C7401 fixture, block "V4": TypeElement is "PROLOCK", not "ZERO ENERGIE" -- the signal
        // lives in the dedicated column V cell (trim/casse variables), totally independent of TypeElement.
        var cells = new Dictionary<string, string?>
        {
            ["K6:T6"] = "C7401",
            ["B19:E20"] = "V1",
            ["H18:U19"] = "Aspiration",
            ["H20:O21"] = "FERMÉE",
            ["B22:E23"] = "PROLOCK",
            ["V18:V19"] = " zero energie ",
            ["B26:E27"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().ContainSingle().Which.HasZeroEnergie.Should().BeTrue();
        result.Points.Should().Contain(new PointPivot(ZeroEnergieColonneName, "C7401-V1"));
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_WithBlankZeroEnergieCell_SetsHasZeroEnergieFalseWithoutWarning()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:T6"] = "C7401",
            ["B19:E20"] = "V1",
            ["H18:U19"] = "Aspiration",
            ["H20:O21"] = "FERMÉE",
            ["B22:E23"] = "PROLOCK",
            ["V18:V19"] = null,
            ["B26:E27"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().ContainSingle().Which.HasZeroEnergie.Should().BeFalse();
        result.Errors.Should().NotContain(e => e.Code == ExtractionErrorCode.UnexpectedZeroEnergieValue);
    }

    [Fact]
    public void Extract_WithUnexpectedZeroEnergieCellValue_SetsHasZeroEnergieFalseAndAddsWarning()
    {
        var cells = new Dictionary<string, string?>
        {
            ["K6:T6"] = "C7401",
            ["B19:E20"] = "V1",
            ["H18:U19"] = "Aspiration",
            ["H20:O21"] = "FERMÉE",
            ["B22:E23"] = "PROLOCK",
            ["V18:V19"] = "0 ENERGIE",
            ["B26:E27"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().ContainSingle().Which.HasZeroEnergie.Should().BeFalse();
        var warning = result.Errors.Should().ContainSingle(e => e.Code == ExtractionErrorCode.UnexpectedZeroEnergieValue).Subject;
        warning.ExtractedValue.Should().Be("0 ENERGIE");
    }

    [Fact]
    public void Extract_WithNoZeroEnergieExpectedValueConfigured_NeverJudgesTheCellContent()
    {
        // A profile without ZeroEnergieExpectedValue must never throw nor warn, even if the cell
        // holds text -- HasZeroEnergie stays false regardless, per the explicit "unchanged behavior"
        // requirement for a profile that hasn't configured this notion yet.
        var sheetRule = new SheetExtractionRule(
            Sheet,
            new RepeatingBlockLocator(Sheet, 19, 7, IsolementFieldNames.Identification,
            [
                new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                new BlockFieldDefinition(IsolementFieldNames.Designation, "H:U", -1, 0),
                new BlockFieldDefinition(IsolementFieldNames.PositionALaPose, "H:O", 1, 2),
                new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4),
                new BlockFieldDefinition(IsolementFieldNames.HasZeroEnergie, "V", -1, 0)
            ]),
            [new ConditionalPointRule(IsolementFieldNames.HasZeroEnergie, ConditionOperator.Equals, "true", ZeroEnergieColonneName)],
            ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], []);
        var cells = new Dictionary<string, string?>
        {
            ["K6:T6"] = "C7401",
            ["B19:E20"] = "V1",
            ["H18:U19"] = "Aspiration",
            ["H20:O21"] = "FERMÉE",
            ["B22:E23"] = "PROLOCK",
            ["V18:V19"] = "ZERO ENERGIE",
            ["B26:E27"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, sheetRule);

        result.Isolements.Should().ContainSingle().Which.HasZeroEnergie.Should().BeFalse();
        result.Errors.Should().NotContain(e => e.Code == ExtractionErrorCode.UnexpectedZeroEnergieValue);
    }

    [Fact]
    public void Extract_WithoutZeroEnergieFieldConfiguredInLocator_LeavesHasZeroEnergieFalseForEveryBlock()
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

        var result = _sut.Extract(workbookReader.Object, CreateSheetRuleWithoutZeroEnergieField());

        result.Isolements.Should().ContainSingle().Which.HasZeroEnergie.Should().BeFalse();
        result.Errors.Should().NotContain(e => e.Code == ExtractionErrorCode.UnexpectedZeroEnergieValue);
    }

    [Fact]
    public void Extract_WithDifferentZeroEnergieExpectedValuesOnTwoProfiles_EachRestitutesItsOwnResult()
    {
        // Anti-hardcoding guard-rail, same pattern as Lot C1's EquipementTypeElementNom test: the
        // expected text must come from the profile, never a service-level constant.
        var cells = new Dictionary<string, string?>
        {
            ["K6:T6"] = "C7401",
            ["B19:E20"] = "V1",
            ["H18:U19"] = "Aspiration",
            ["H20:O21"] = "FERMÉE",
            ["B22:E23"] = "PROLOCK",
            ["V18:V19"] = "0 ENERGIE",
            ["B26:E27"] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var ruleExpectingZeroEnergie = CreateSheetRule();
        var ruleExpectingZeroDigitEnergie = new SheetExtractionRule(
            Sheet,
            new RepeatingBlockLocator(Sheet, 19, 7, IsolementFieldNames.Identification,
            [
                new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                new BlockFieldDefinition(IsolementFieldNames.Designation, "H:U", -1, 0),
                new BlockFieldDefinition(IsolementFieldNames.PositionALaPose, "H:O", 1, 2),
                new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4),
                new BlockFieldDefinition(IsolementFieldNames.HasZeroEnergie, "V", -1, 0)
            ]),
            [new ConditionalPointRule(IsolementFieldNames.HasZeroEnergie, ConditionOperator.Equals, "true", ZeroEnergieColonneName)],
            ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], [], zeroEnergieExpectedValue: "0 ENERGIE");

        var resultExpectingZeroEnergie = _sut.Extract(workbookReader.Object, ruleExpectingZeroEnergie);
        var resultExpectingZeroDigitEnergie = _sut.Extract(workbookReader.Object, ruleExpectingZeroDigitEnergie);

        resultExpectingZeroEnergie.Isolements.Single().HasZeroEnergie.Should().BeFalse();
        resultExpectingZeroEnergie.Errors.Should().Contain(e => e.Code == ExtractionErrorCode.UnexpectedZeroEnergieValue);

        resultExpectingZeroDigitEnergie.Isolements.Single().HasZeroEnergie.Should().BeTrue();
        resultExpectingZeroDigitEnergie.Errors.Should().NotContain(e => e.Code == ExtractionErrorCode.UnexpectedZeroEnergieValue);
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
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.NoConditionalPointCreated);
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
        // ENERGIE"), also gets an NoConditionalPointCreated warning from the non-matching conditional
        // Colonne group -- the same evaluator behavior already established in Lot B4 (e.g. a TUBING
        // isolement not matching AUTRES JOINTS TOUCHES' "Pose Étiquettes" rule also warns).
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().ContainSingle(e => e.Code == ExtractionErrorCode.RequiredFieldMissing);
        result.Errors.Should().ContainSingle(e => e.Code == ExtractionErrorCode.NoConditionalPointCreated);
    }
}
