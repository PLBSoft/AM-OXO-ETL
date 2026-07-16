using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Primitives;

public class RepeatingBlockLocatorTests
{
    private static IReadOnlyList<BlockFieldDefinition> ValidFields =>
    [
        new BlockFieldDefinition("Identification", "B:E", 0, 1)
    ];

    [Fact]
    public void Constructor_WithValidArguments_CreatesRepeatingBlockLocator()
    {
        var fields = ValidFields;

        var locator = new RepeatingBlockLocator("ISOLEMENT", 19, 7, "Identification", fields);

        locator.Sheet.Should().Be("ISOLEMENT");
        locator.FirstBlockStartRow.Should().Be(19);
        locator.Step.Should().Be(7);
        locator.StopFieldName.Should().Be("Identification");
        locator.Fields.Should().BeEquivalentTo(fields);
    }

    [Fact]
    public void Constructor_WithSameArguments_ButDifferentFieldsListInstances_ProducesStructurallyEqualInstances()
    {
        var first = new RepeatingBlockLocator("ISOLEMENT", 19, 7, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);
        var second = new RepeatingBlockLocator("ISOLEMENT", 19, 7, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Constructor_WithDifferentFields_ProducesUnequalInstances()
    {
        var first = new RepeatingBlockLocator("ISOLEMENT", 19, 7, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);
        var second = new RepeatingBlockLocator("ISOLEMENT", 19, 7, "Identification",
            [new BlockFieldDefinition("Identification", "B:F", 0, 1)]);

        first.Should().NotBe(second);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidSheet_ThrowsDomainValidationException(string? invalidSheet)
    {
        var act = () => new RepeatingBlockLocator(invalidSheet!, 19, 7, "Identification", ValidFields);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sheet")
            .Which.ErrorCode.Should().Be(DomainErrorCode.RepeatingBlockLocator_EmptySheet);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveFirstBlockStartRow_ThrowsDomainArgumentOutOfRangeException(int invalidRow)
    {
        var act = () => new RepeatingBlockLocator("ISOLEMENT", invalidRow, 7, "Identification", ValidFields);

        act.Should().Throw<DomainArgumentOutOfRangeException>()
            .WithParameterName("firstBlockStartRow")
            .Which.ErrorCode.Should().Be(DomainErrorCode.RepeatingBlockLocator_NonPositiveFirstBlockStartRow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveStep_ThrowsDomainArgumentOutOfRangeException(int invalidStep)
    {
        var act = () => new RepeatingBlockLocator("ISOLEMENT", 19, invalidStep, "Identification", ValidFields);

        act.Should().Throw<DomainArgumentOutOfRangeException>()
            .WithParameterName("step")
            .Which.ErrorCode.Should().Be(DomainErrorCode.RepeatingBlockLocator_NonPositiveStep);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidStopFieldName_ThrowsDomainValidationException(string? invalidStopFieldName)
    {
        var act = () => new RepeatingBlockLocator("ISOLEMENT", 19, 7, invalidStopFieldName!, ValidFields);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("stopFieldName")
            .Which.ErrorCode.Should().Be(DomainErrorCode.RepeatingBlockLocator_EmptyStopFieldName);
    }

    [Fact]
    public void Constructor_WithNullFields_ThrowsArgumentNullException()
    {
        var act = () => new RepeatingBlockLocator("ISOLEMENT", 19, 7, "Identification", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyFields_ThrowsDomainValidationException()
    {
        var act = () => new RepeatingBlockLocator("ISOLEMENT", 19, 7, "Identification", []);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("fields")
            .Which.ErrorCode.Should().Be(DomainErrorCode.RepeatingBlockLocator_EmptyFields);
    }

    [Fact]
    public void Constructor_CoversPas1ForProcedureAndPas3789ForIsolementSheets()
    {
        int[] confirmedSteps = [1, 3, 7, 8];

        foreach (var step in confirmedSteps)
        {
            var locator = new RepeatingBlockLocator("SHEET", 1, step, "StopField", ValidFields);
            locator.Step.Should().Be(step);
        }
    }
}
