using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Moq;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo;

public class RepeatingBlockReaderTests
{
    private readonly RepeatingBlockReader _sut = new();

    private static Mock<IWorkbookReader> CreateWorkbookReader(IReadOnlyDictionary<(string Sheet, string Range), string?> cells)
    {
        var mock = new Mock<IWorkbookReader>();
        mock.Setup(r => r.ReadCellValue(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string sheet, string range) => cells.GetValueOrDefault((sheet, range)));
        return mock;
    }

    [Fact]
    public void Read_WithMultipleFieldsAndValidBlocks_ComputesCorrectRangesPerBlock()
    {
        var locator = new RepeatingBlockLocator("ISOLEMENT", 19, 7, "Identification",
        [
            new BlockFieldDefinition("Identification", "B:E", 0, 1),
            new BlockFieldDefinition("Designation", "H:U", -1, 0)
        ]);
        var cells = new Dictionary<(string, string), string?>
        {
            [("ISOLEMENT", "B19:E20")] = "ISO1",
            [("ISOLEMENT", "H18:U19")] = "Vanne 1",
            [("ISOLEMENT", "B26:E27")] = "ISO2",
            [("ISOLEMENT", "H25:U26")] = "Vanne 2",
            [("ISOLEMENT", "B33:E34")] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Read(locator, workbookReader.Object);

        result.Errors.Should().BeEmpty();
        result.Blocks.Should().HaveCount(2);
        result.Blocks[0].Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Identification"] = "ISO1",
            ["Designation"] = "Vanne 1"
        });
        result.Blocks[1].Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Identification"] = "ISO2",
            ["Designation"] = "Vanne 2"
        });
    }

    [Fact]
    public void Read_StopsAtFirstEmptyStopField_WithoutReadingOtherFieldsOfTheStoppedBlock()
    {
        var locator = new RepeatingBlockLocator("ISOLEMENT", 19, 7, "Identification",
        [
            new BlockFieldDefinition("Identification", "B:E", 0, 1),
            new BlockFieldDefinition("Designation", "H:U", -1, 0)
        ]);
        var cells = new Dictionary<(string, string), string?>
        {
            [("ISOLEMENT", "B19:E20")] = "ISO1",
            [("ISOLEMENT", "H18:U19")] = "Vanne 1",
            [("ISOLEMENT", "B26:E27")] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Read(locator, workbookReader.Object);

        result.Blocks.Should().HaveCount(1);
        workbookReader.Verify(r => r.ReadCellValue("ISOLEMENT", "H25:U26"), Times.Never);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    public void Read_WithConfirmedSteps_ReadsExpectedNumberOfBlocksBeforeStopping(int step)
    {
        var locator = new RepeatingBlockLocator("SHEET", 9, step, "Action",
        [
            new BlockFieldDefinition("Action", "C", 0, 0)
        ]);
        var cells = new Dictionary<(string, string), string?>
        {
            [("SHEET", $"C{9}")] = "Action 1",
            [("SHEET", $"C{9 + step}")] = "Action 2",
            [("SHEET", $"C{9 + 2 * step}")] = "Action 3",
            [("SHEET", $"C{9 + 3 * step}")] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Read(locator, workbookReader.Object);

        result.Errors.Should().BeEmpty();
        result.Blocks.Should().HaveCount(3);
        result.Blocks[0]["Action"].Should().Be("Action 1");
        result.Blocks[2]["Action"].Should().Be("Action 3");
    }

    [Fact]
    public void Read_WithNonStopFieldBlankWhileStopFieldPopulated_ReportsErrorSkipsBlockAndContinues()
    {
        var locator = new RepeatingBlockLocator("ISOLEMENT", 19, 7, "Identification",
        [
            new BlockFieldDefinition("Identification", "B:E", 0, 1),
            new BlockFieldDefinition("Designation", "H:U", -1, 0)
        ]);
        var cells = new Dictionary<(string, string), string?>
        {
            [("ISOLEMENT", "B19:E20")] = "ISO1",
            [("ISOLEMENT", "H18:U19")] = null,
            [("ISOLEMENT", "B26:E27")] = "ISO2",
            [("ISOLEMENT", "H25:U26")] = "Vanne 2",
            [("ISOLEMENT", "B33:E34")] = null
        };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Read(locator, workbookReader.Object);

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.RequiredFieldMissing);
        result.Blocks.Should().ContainSingle().Which.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Identification"] = "ISO2",
            ["Designation"] = "Vanne 2"
        });
    }
}
