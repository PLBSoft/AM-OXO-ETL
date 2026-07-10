using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Entities;

public class SheetConfigTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesSheetConfig()
    {
        var sheet = new SheetConfig("Summary", sheetIndex: 0);

        sheet.SheetName.Should().Be("Summary");
        sheet.SheetIndex.Should().Be(0);
        sheet.Id.Should().NotBeEmpty();
        sheet.CellMappings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidSheetName_ThrowsArgumentException(string? invalidSheetName)
    {
        var act = () => new SheetConfig(invalidSheetName!, sheetIndex: 0);

        act.Should().Throw<ArgumentException>().WithParameterName("sheetName");
    }

    [Fact]
    public void Constructor_WithNegativeSheetIndex_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new SheetConfig("Summary", sheetIndex: -1);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("sheetIndex");
    }

    [Fact]
    public void AddCellMapping_WithNewMapping_AddsToCollection()
    {
        var sheet = new SheetConfig("Summary", sheetIndex: 0);
        var mapping = new CellMapping("B4", "InvoiceNumber", CellDataType.Text);

        sheet.AddCellMapping(mapping);

        sheet.CellMappings.Should().ContainSingle().Which.Should().Be(mapping);
    }

    [Fact]
    public void AddCellMapping_WithDuplicateTargetPropertyName_ThrowsInvalidOperationException()
    {
        var sheet = new SheetConfig("Summary", sheetIndex: 0);
        sheet.AddCellMapping(new CellMapping("B4", "InvoiceNumber", CellDataType.Text));

        var act = () => sheet.AddCellMapping(new CellMapping("C4", "InvoiceNumber", CellDataType.Text));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*InvoiceNumber*");
    }

    [Fact]
    public void AddCellMapping_WithNull_ThrowsArgumentNullException()
    {
        var sheet = new SheetConfig("Summary", sheetIndex: 0);

        var act = () => sheet.AddCellMapping(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
