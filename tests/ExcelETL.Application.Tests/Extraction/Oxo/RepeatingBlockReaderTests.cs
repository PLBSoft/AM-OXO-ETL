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
        var locator = new RepeatingBlockLocator(19, 7,
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

        var result = _sut.Read(locator, "ISOLEMENT", "Identification", workbookReader.Object);

        result.Errors.Should().BeEmpty();
        result.Blocks.Should().HaveCount(2);
        result.Blocks[0].StartRow.Should().Be(19);
        result.Blocks[0].Fields.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Identification"] = "ISO1",
            ["Designation"] = "Vanne 1"
        });
        result.Blocks[1].StartRow.Should().Be(26);
        result.Blocks[1].Fields.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Identification"] = "ISO2",
            ["Designation"] = "Vanne 2"
        });
    }

    [Fact]
    public void Read_StopsAtFirstEmptyStopField_WithoutReadingOtherFieldsOfTheStoppedBlock()
    {
        var locator = new RepeatingBlockLocator(19, 7,
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

        var result = _sut.Read(locator, "ISOLEMENT", "Identification", workbookReader.Object);

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
        var locator = new RepeatingBlockLocator(9, step,
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

        var result = _sut.Read(locator, "SHEET", "Action", workbookReader.Object);

        result.Errors.Should().BeEmpty();
        result.Blocks.Should().HaveCount(3);
        result.Blocks[0].Fields["Action"].Should().Be("Action 1");
        result.Blocks[2].Fields["Action"].Should().Be("Action 3");
    }

    // Lot 084 (G12): the stop field is the caller's, not a setting of the locator.
    [Fact]
    public void Read_StopsOnTheFieldTheCallerNames()
    {
        var locator = new RepeatingBlockLocator(19, 7,
        [
            new BlockFieldDefinition("Identification", "B:E", 0, 1, isRequired: false),
            new BlockFieldDefinition("Designation", "H:U", -1, 0)
        ]);
        var cells = new Dictionary<(string, string), string?>
        {
            [("ISOLEMENT", "B19:E20")] = null,
            [("ISOLEMENT", "H18:U19")] = "Vanne 1",
            [("ISOLEMENT", "H25:U26")] = null
        };

        var result = _sut.Read(locator, "ISOLEMENT", "Designation", CreateWorkbookReader(cells).Object);

        result.Blocks.Should().ContainSingle().Which.Fields["Designation"].Should().Be("Vanne 1");
    }

    [Fact]
    public void Read_WithAStopFieldOutsideTheBlock_ThrowsUnknownFieldReferenceException()
    {
        var act = () => _sut.Read(PlatinesLocator(), "PLATINES", "Action", CreateWorkbookReader(new Dictionary<(string, string), string?>()).Object);

        act.Should().Throw<UnknownFieldReferenceException>();
    }

    [Fact]
    public void Read_WithNonStopFieldBlankWhileStopFieldPopulated_ReportsErrorSkipsBlockAndContinues()
    {
        var locator = new RepeatingBlockLocator(19, 7,
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

        var result = _sut.Read(locator, "ISOLEMENT", "Identification", workbookReader.Object);

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ExtractionErrorCode.RequiredFieldMissing);
        var block = result.Blocks.Should().ContainSingle().Subject;
        // The one dropped block (ISO1, row 19) still consumed a row -- the surviving block's StartRow
        // must reflect its real position (26), not its index (0) within the (now shorter) Blocks list.
        block.StartRow.Should().Be(26);
        block.Fields.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Identification"] = "ISO2",
            ["Designation"] = "Vanne 2"
        });
    }

    // Lot 084.3 (G4): an optional field left blank keeps the block, read as "".
    private static RepeatingBlockLocator PlatinesLocator() => new(17, 8,
    [
        new BlockFieldDefinition("Identification", "B:E", 0, 1),
        new BlockFieldDefinition("TypeElement", "B:E", 3, 5),
        new BlockFieldDefinition("PoseeLe", "H:N", 2, 2, isRequired: false)
    ]);

    [Fact]
    public void Read_WithBlankOptionalField_KeepsTheBlock_WithAnEmptyValue_AndNoError()
    {
        var cells = new Dictionary<(string, string), string?>
        {
            [("PLATINES", "B17:E18")] = "PT1",
            [("PLATINES", "B20:E22")] = "PLATINE",
            [("PLATINES", "H19:N19")] = null,
            [("PLATINES", "B25:E26")] = null
        };

        var result = _sut.Read(PlatinesLocator(), "PLATINES", "Identification", CreateWorkbookReader(cells).Object);

        result.Errors.Should().BeEmpty();
        result.Blocks.Should().ContainSingle().Which.Fields.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Identification"] = "PT1",
            ["TypeElement"] = "PLATINE",
            ["PoseeLe"] = ""
        });
    }

    [Fact]
    public void Read_WithFilledOptionalField_KeepsItsValue()
    {
        var cells = new Dictionary<(string, string), string?>
        {
            [("PLATINES", "B17:E18")] = "PT1",
            [("PLATINES", "B20:E22")] = "PLATINE",
            [("PLATINES", "H19:N19")] = "DEBUT MAD",
            [("PLATINES", "B25:E26")] = null
        };

        var result = _sut.Read(PlatinesLocator(), "PLATINES", "Identification", CreateWorkbookReader(cells).Object);

        result.Blocks.Should().ContainSingle().Which.Fields["PoseeLe"].Should().Be("DEBUT MAD");
    }

    [Fact]
    public void Read_WithBlankRequiredField_StillDropsTheBlock_EvenWhenAnOptionalFieldIsBlankToo()
    {
        var cells = new Dictionary<(string, string), string?>
        {
            [("PLATINES", "B17:E18")] = "PT1",
            [("PLATINES", "B20:E22")] = null,
            [("PLATINES", "H19:N19")] = null,
            [("PLATINES", "B25:E26")] = null
        };

        var result = _sut.Read(PlatinesLocator(), "PLATINES", "Identification", CreateWorkbookReader(cells).Object);

        result.Blocks.Should().BeEmpty();
        var error = result.Errors.Should().ContainSingle().Which;
        error.Code.Should().Be(ExtractionErrorCode.RequiredFieldMissing);
        error.Message.Should().Contain("'TypeElement'").And.NotContain("PoseeLe");
    }

    [Fact]
    public void Read_WithOptionalFields_StillReadsTheStopFieldFirst()
    {
        var cells = new Dictionary<(string, string), string?> { [("PLATINES", "B17:E18")] = null };
        var workbookReader = CreateWorkbookReader(cells);

        var result = _sut.Read(PlatinesLocator(), "PLATINES", "Identification", workbookReader.Object);

        result.Blocks.Should().BeEmpty();
        workbookReader.Verify(r => r.ReadCellValue("PLATINES", It.IsAny<string>()), Times.Once);
    }
}
