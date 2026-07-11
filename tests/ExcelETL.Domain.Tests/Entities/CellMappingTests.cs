using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Enums;
using ExcelETL.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Entities;

public class CellMappingTests
{
    [Theory]
    [InlineData("B4", "InvoiceNumber", CellDataType.Text)]
    [InlineData("AB123", "Total", CellDataType.Decimal)]
    [InlineData("B4:D4", "MergedHeader", CellDataType.Text)]
    public void Constructor_WithValidArguments_CreatesCellMapping(
        string sourceCell, string targetPropertyName, CellDataType dataType)
    {
        var mapping = new CellMapping(sourceCell, targetPropertyName, dataType);

        mapping.SourceCell.Should().Be(sourceCell);
        mapping.TargetPropertyName.Should().Be(targetPropertyName);
        mapping.DataType.Should().Be(dataType);
        mapping.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("ZZZZ1")]
    [InlineData("B4:")]
    [InlineData("B4-D4")]
    public void Constructor_WithInvalidSourceCell_ThrowsArgumentException(string? invalidSourceCell)
    {
        var act = () => new CellMapping(invalidSourceCell!, "TargetProperty", CellDataType.Text);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sourceCell")
            .Which.ErrorCode.Should().Be(DomainErrorCode.CellMapping_InvalidSourceCell);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidTargetPropertyName_ThrowsArgumentException(string? invalidTargetPropertyName)
    {
        var act = () => new CellMapping("B4", invalidTargetPropertyName!, CellDataType.Text);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("targetPropertyName")
            .Which.ErrorCode.Should().Be(DomainErrorCode.CellMapping_EmptyTargetPropertyName);
    }
}
