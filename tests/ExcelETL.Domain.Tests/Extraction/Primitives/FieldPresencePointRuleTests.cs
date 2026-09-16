using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Primitives;

public class FieldPresencePointRuleTests
{
    private static BlockFieldDefinition CreateCell() => new("PoseeLe", "H:N", 2, 2);

    [Fact]
    public void Constructor_WithValidArguments_CreatesFieldPresencePointRule()
    {
        var cell = CreateCell();

        var rule = new FieldPresencePointRule(cell, "RECEPTION DEBUT MAD");

        rule.Cell.Should().Be(cell);
        rule.ColonneName.Should().Be("RECEPTION DEBUT MAD");
    }

    [Fact]
    public void Constructor_WithSameArguments_ProducesStructurallyEqualInstances()
    {
        var first = new FieldPresencePointRule(CreateCell(), "RECEPTION DEBUT MAD");
        var second = new FieldPresencePointRule(CreateCell(), "RECEPTION DEBUT MAD");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Constructor_WithoutExpectedValue_LeavesExpectedValueNull()
    {
        var rule = new FieldPresencePointRule(CreateCell(), "RECEPTION DEBUT MAD");

        rule.ExpectedValue.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithExpectedValue_StoresIt()
    {
        var rule = new FieldPresencePointRule(CreateCell(), "RECEPTION DEBUT MAD", "DEBUT MAD");

        rule.ExpectedValue.Should().Be("DEBUT MAD");
    }

    [Fact]
    public void Constructor_WithDifferentExpectedValues_ProducesUnequalInstances()
    {
        var first = new FieldPresencePointRule(CreateCell(), "RECEPTION DEBUT MAD", "DEBUT MAD");
        var second = new FieldPresencePointRule(CreateCell(), "RECEPTION DEBUT MAD", "FIN MAD");

        first.Should().NotBe(second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankButNonNullExpectedValue_ThrowsDomainValidationException(string blankExpectedValue)
    {
        var act = () => new FieldPresencePointRule(CreateCell(), "RECEPTION DEBUT MAD", blankExpectedValue);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("expectedValue")
            .Which.ErrorCode.Should().Be(DomainErrorCode.FieldPresencePointRule_BlankExpectedValue);
    }

    [Fact]
    public void Constructor_WithNullCell_ThrowsArgumentNullException()
    {
        var act = () => new FieldPresencePointRule(null!, "RECEPTION DEBUT MAD");

        act.Should().Throw<ArgumentNullException>().WithParameterName("cell");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidColonneName_ThrowsDomainValidationException(string? invalidColonneName)
    {
        var act = () => new FieldPresencePointRule(CreateCell(), invalidColonneName!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("colonneName")
            .Which.ErrorCode.Should().Be(DomainErrorCode.FieldPresencePointRule_EmptyColonneName);
    }
}
