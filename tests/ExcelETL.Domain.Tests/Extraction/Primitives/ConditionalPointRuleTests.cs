using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Primitives;

public class ConditionalPointRuleTests
{
    [Theory]
    [InlineData(ConditionOperator.Equals, "SOUPAPE")]
    [InlineData(ConditionOperator.NotEquals, "TUBING")]
    public void Constructor_WithValidArguments_CreatesConditionalPointRule(ConditionOperator @operator, string comparisonValue)
    {
        var rule = new ConditionalPointRule("TypeElement", @operator, comparisonValue, "SOUPAPE : CONSTAT ENCRASSEMENT");

        rule.SourceFieldName.Should().Be("TypeElement");
        rule.Operator.Should().Be(@operator);
        rule.ComparisonValue.Should().Be(comparisonValue);
        rule.ColonneName.Should().Be("SOUPAPE : CONSTAT ENCRASSEMENT");
    }

    [Fact]
    public void Constructor_WithSameArguments_ProducesStructurallyEqualInstances()
    {
        var first = new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "Colonne");
        var second = new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "Colonne");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidSourceFieldName_ThrowsDomainValidationException(string? invalidSourceFieldName)
    {
        var act = () => new ConditionalPointRule(invalidSourceFieldName!, ConditionOperator.Equals, "SOUPAPE", "Colonne");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("sourceFieldName")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ConditionalPointRule_EmptySourceFieldName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidComparisonValue_ThrowsDomainValidationException(string? invalidComparisonValue)
    {
        var act = () => new ConditionalPointRule("TypeElement", ConditionOperator.Equals, invalidComparisonValue!, "Colonne");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("comparisonValue")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ConditionalPointRule_EmptyComparisonValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidColonneName_ThrowsDomainValidationException(string? invalidColonneName)
    {
        var act = () => new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", invalidColonneName!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("colonneName")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ConditionalPointRule_EmptyColonneName);
    }
}
