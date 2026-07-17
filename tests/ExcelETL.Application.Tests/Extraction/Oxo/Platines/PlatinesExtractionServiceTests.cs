using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Platines;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo.Platines;

public class PlatinesExtractionServiceTests
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

    private readonly PlatinesExtractionService _sut = new(new RepeatingBlockReader(), new TextTransformEvaluator());

    private static SheetExtractionRule CreateSheetRule() => new(
        Sheet,
        new RepeatingBlockLocator(Sheet, 17, 8, PlatinesFieldNames.Identification,
        [
            new BlockFieldDefinition(PlatinesFieldNames.Identification, "B:E", 0, 1),
            new BlockFieldDefinition(PlatinesFieldNames.Designation, "H:V", -1, 0),
            new BlockFieldDefinition(PlatinesFieldNames.TypeElement, "B:E", 3, 5)
        ]),
        [],
        UnconditionalColonneNames);

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
}
