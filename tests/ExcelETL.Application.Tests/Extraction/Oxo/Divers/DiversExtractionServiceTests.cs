using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Divers;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo.Divers;

public class DiversExtractionServiceTests
{
    private const string Sheet = "DIVERS";

    private const string InstrumentationColonne = "SYNCHRONISATION INSTRUMENTATION";
    private const string ZeroEnergieColonne = "ZÉRO ENERGIE EN PRESENCE EE";
    private const string SoupapeConstatColonne = "SOUPAPE : CONSTAT ENCRASSEMENT";
    private const string SoupapeReceptionColonne = "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS";
    private const string PfSignatureColonne = "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES";
    private const string PfValidationColonne = "PF : VALIDATION CONSTAT ENCRASSEMENT";
    private const string PfAccordColonne = "PF : ACCORD TRAVAUX FEU";

    private readonly DiversExtractionService _sut =
        new(new RepeatingBlockReader(), new TextTransformEvaluator(), new ConditionalPointRuleEvaluator());

    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 9, 3, IsolementFieldNames.Identification,
        [
            new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:G", 0, 2),
            new BlockFieldDefinition(IsolementFieldNames.Identification, "H:K", 0, 2),
            new BlockFieldDefinition(IsolementFieldNames.Designation, "L:V", 0, 2)
        ]),
        [
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "INSTRUMENTATION", InstrumentationColonne),
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", ZeroEnergieColonne),
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", SoupapeConstatColonne),
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", SoupapeReceptionColonne),
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", PfSignatureColonne),
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", PfValidationColonne),
            new ConditionalPointRule(IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", PfAccordColonne)
        ],
        []);

    private static Mock<IWorkbookReader> CreateWorkbookReader(IReadOnlyDictionary<string, string?> cells)
    {
        var mock = new Mock<IWorkbookReader>();
        mock.Setup(r => r.ReadCellValue(Sheet, It.IsAny<string>()))
            .Returns((string _, string range) => cells.GetValueOrDefault(range));
        return mock;
    }

    private static Dictionary<string, string?> BaseCells(string typeElement) => new()
    {
        ["N6"] = "G6306B",
        ["B6:E6"] = "ZONE 4",
        ["B9:G11"] = typeElement,
        ["H9:K11"] = "LT6306",
        ["L9:V11"] = "Transmetteur de niveau",
        ["H12:K14"] = null
    };

    [Fact]
    public void Extract_ReadsLoc1AsRawValueWithNoTransformation()
    {
        var cells = BaseCells("INSTRUMENTATION");
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Loc1.Should().Be("ZONE 4");
    }

    [Fact]
    public void Extract_WithInstrumentationType_CreatesOnlySynchronisationPoint()
    {
        var cells = BaseCells("INSTRUMENTATION");
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().ContainSingle().Which.Repere.Should().Be("G6306B-LT6306");
        result.Points.Should().ContainSingle().Which.ColonneNom.Should().Be(InstrumentationColonne);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_WithSoupapeType_CreatesBothSoupapePointsAndNoWarning()
    {
        var cells = BaseCells("SOUPAPE");
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Points.Select(p => p.ColonneNom).Should().BeEquivalentTo([SoupapeConstatColonne, SoupapeReceptionColonne]);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_WithSoupapeTypeTrailingSpace_StillMatchesBothColonnes()
    {
        // Real G6306B fixture: the cell literally contains "SOUPAPE " (trailing space).
        var cells = BaseCells("SOUPAPE ");
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Points.Should().HaveCount(2);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_WithPointFeuType_CreatesAllThreePfPoints()
    {
        var cells = BaseCells("POINT FEU");
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Points.Select(p => p.ColonneNom).Should().BeEquivalentTo([PfSignatureColonne, PfValidationColonne, PfAccordColonne]);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Extract_WithPointDeFeuSpellingVariant_CreatesNoPointsAndOneAggregateWarning()
    {
        // Real G6306B fixture: the cell literally contains "POINT DE FEU" (with "DE"), which the
        // client confirmed is a genuine spelling mismatch against the retained "POINT FEU" -- not
        // normalized away by trim/casing. Must produce exactly ONE warning across the 7 conditional
        // Colonnes, not one per non-matching group (the ConditionalPointGroupEvaluator fix).
        var cells = BaseCells("POINT DE FEU");
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Extract(workbookReader.Object, CreateSheetRule());

        result.Isolements.Should().ContainSingle();
        result.Points.Should().BeEmpty();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.UnrecognizedTypeElement);
    }

    [Fact]
    public void Extract_StopsAtFirstBlankIdentification_WithoutReadingBeyond()
    {
        var cells = BaseCells("INSTRUMENTATION");
        var workbookReader = CreateWorkbookReader(cells);

        _sut.Extract(workbookReader.Object, CreateSheetRule());

        workbookReader.Verify(r => r.ReadCellValue(Sheet, "L12:V14"), Times.Never);
    }
}
