using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Primitives;

public class DirectCellTests
{
    [Theory]
    [InlineData("PROCEDURE", "M2")]
    [InlineData("PROCEDURE", "M2:O2")]
    [InlineData("ISOLEMENT", "K6:T6")]
    public void Constructor_WithValidArguments_CreatesDirectCell(string sheet, string range)
    {
        var cell = new DirectCell(sheet, range);

        cell.Sheet.Should().Be(sheet);
        cell.Range.Should().Be(range);
    }

    [Fact]
    public void Constructor_WithSameArguments_ProducesStructurallyEqualInstances()
    {
        var first = new DirectCell("PROCEDURE", "M2:O2");
        var second = new DirectCell("PROCEDURE", "M2:O2");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidSheet_ThrowsDomainValidationException(string? invalidSheet)
    {
        var act = () => new DirectCell(invalidSheet!, "M2:O2");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sheet")
            .Which.ErrorCode.Should().Be(DomainErrorCode.DirectCell_EmptySheet);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("M2:")]
    [InlineData("M2-O2")]
    public void Constructor_WithInvalidRange_ThrowsDomainValidationException(string? invalidRange)
    {
        var act = () => new DirectCell("PROCEDURE", invalidRange!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("range")
            .Which.ErrorCode.Should().Be(DomainErrorCode.DirectCell_InvalidRange);
    }
}
