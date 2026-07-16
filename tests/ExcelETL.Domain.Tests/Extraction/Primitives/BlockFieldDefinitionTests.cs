using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Primitives;

public class BlockFieldDefinitionTests
{
    [Theory]
    [InlineData("Identification", "B:E", 0, 1)]
    [InlineData("Designation", "H:U", -1, 0)]
    [InlineData("Ordre", "B", 0, 0)]
    public void Constructor_WithValidArguments_CreatesBlockFieldDefinition(
        string name, string columnRange, int rowOffsetStart, int rowOffsetEnd)
    {
        var field = new BlockFieldDefinition(name, columnRange, rowOffsetStart, rowOffsetEnd);

        field.Name.Should().Be(name);
        field.ColumnRange.Should().Be(columnRange);
        field.RowOffsetStart.Should().Be(rowOffsetStart);
        field.RowOffsetEnd.Should().Be(rowOffsetEnd);
    }

    [Fact]
    public void Constructor_WithSameArguments_ProducesStructurallyEqualInstances()
    {
        var first = new BlockFieldDefinition("Identification", "B:E", 0, 1);
        var second = new BlockFieldDefinition("Identification", "B:E", 0, 1);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidName_ThrowsDomainValidationException(string? invalidName)
    {
        var act = () => new BlockFieldDefinition(invalidName!, "B:E", 0, 1);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("name")
            .Which.ErrorCode.Should().Be(DomainErrorCode.BlockFieldDefinition_EmptyName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("B:E:H")]
    [InlineData("b:e")]
    public void Constructor_WithInvalidColumnRange_ThrowsDomainValidationException(string? invalidColumnRange)
    {
        var act = () => new BlockFieldDefinition("Identification", invalidColumnRange!, 0, 1);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("columnRange")
            .Which.ErrorCode.Should().Be(DomainErrorCode.BlockFieldDefinition_InvalidColumnRange);
    }

    [Fact]
    public void Constructor_WithRowOffsetEndBeforeStart_ThrowsDomainValidationException()
    {
        var act = () => new BlockFieldDefinition("Identification", "B:E", 2, 1);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("rowOffsetEnd")
            .Which.ErrorCode.Should().Be(DomainErrorCode.BlockFieldDefinition_RowOffsetEndBeforeStart);
    }
}
