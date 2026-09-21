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

    // Lot 084.1 (G2)
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithIsNotBlankAndNoComparisonValue_StoresNull(string? comparisonValue)
    {
        var rule = new ConditionalPointRule("PoseeLe", ConditionOperator.IsNotBlank, comparisonValue, "RECEPTION DEBUT MAD");

        rule.Operator.Should().Be(ConditionOperator.IsNotBlank);
        rule.ComparisonValue.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithIsNotBlankAndAComparisonValue_ThrowsDomainValidationException()
    {
        var act = () => new ConditionalPointRule("PoseeLe", ConditionOperator.IsNotBlank, "DEBUT MAD", "RECEPTION DEBUT MAD");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("comparisonValue")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ConditionalPointRule_ComparisonValueNotAllowedForIsNotBlank);
    }

    [Fact]
    public void Constructor_WithNotEqualsAndNoComparisonValue_ThrowsDomainValidationException()
    {
        var act = () => new ConditionalPointRule("TypeElement", ConditionOperator.NotEquals, null, "Colonne");

        act.Should().Throw<DomainValidationException>()
            .Which.ErrorCode.Should().Be(DomainErrorCode.ConditionalPointRule_EmptyComparisonValue);
    }
}
