using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo;

public class ConditionalPointRuleEvaluatorTests
{
    private readonly ConditionalPointRuleEvaluator _sut = new();

    [Fact]
    public void Evaluate_WithNoRules_AlwaysCreatesPoint()
    {
        var extractedFields = new Dictionary<string, string> { ["TypeElement"] = "SOUPAPE" };

        var (shouldCreate, warning) = _sut.Evaluate([], extractedFields);

        shouldCreate.Should().BeTrue();
        warning.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WithMatchingEqualsRule_CreatesPoint()
    {
        var rule = new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE (PS941)");
        var extractedFields = new Dictionary<string, string> { ["TypeElement"] = "ZERO ENERGIE" };

        var (shouldCreate, warning) = _sut.Evaluate([rule], extractedFields);

        shouldCreate.Should().BeTrue();
        warning.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WithMatchingNotEqualsRule_CreatesPoint()
    {
        var rule = new ConditionalPointRule("TypeElement", ConditionOperator.NotEquals, "TUBING", "POSE ÉTIQUETTES");
        var extractedFields = new Dictionary<string, string> { ["TypeElement"] = "COLLIER" };

        var (shouldCreate, warning) = _sut.Evaluate([rule], extractedFields);

        shouldCreate.Should().BeTrue();
        warning.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WithNonMatchingNotEqualsRule_DoesNotCreatePoint()
    {
        var rule = new ConditionalPointRule("TypeElement", ConditionOperator.NotEquals, "TUBING", "POSE ÉTIQUETTES");
        var extractedFields = new Dictionary<string, string> { ["TypeElement"] = "TUBING" };

        var (shouldCreate, warning) = _sut.Evaluate([rule], extractedFields);

        shouldCreate.Should().BeFalse();
        warning.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_WithRulesButNoneMatch_DoesNotCreatePointAndReturnsNonBlockingWarning()
    {
        var rules = new[]
        {
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "INSTRUMENTATION", "SYNCHRONISATION INSTRUMENTATION"),
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : CONSTAT ENCRASSEMENT")
        };
        var extractedFields = new Dictionary<string, string> { ["TypeElement"] = "ZERO ENERGIE" };

        var (shouldCreate, warning) = _sut.Evaluate(rules, extractedFields);

        shouldCreate.Should().BeFalse();
        warning.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Evaluate_WithMultipleRulesAndOneMatches_CreatesPoint()
    {
        var rules = new[]
        {
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "INSTRUMENTATION", "SYNCHRONISATION INSTRUMENTATION"),
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : CONSTAT ENCRASSEMENT")
        };
        var extractedFields = new Dictionary<string, string> { ["TypeElement"] = "SOUPAPE" };

        var (shouldCreate, warning) = _sut.Evaluate(rules, extractedFields);

        shouldCreate.Should().BeTrue();
        warning.Should().BeNull();
    }

    [Theory]
    [InlineData("TUBING ")]
    [InlineData("tubing")]
    public void Evaluate_WithNotEqualsRuleAndTrailingSpaceOrCaseVariance_StillTreatsAsEqualAndDoesNotCreatePoint(string actualValue)
    {
        var rule = new ConditionalPointRule("TypeElement", ConditionOperator.NotEquals, "TUBING", "POSE ÉTIQUETTES");
        var extractedFields = new Dictionary<string, string> { ["TypeElement"] = actualValue };

        var (shouldCreate, warning) = _sut.Evaluate([rule], extractedFields);

        shouldCreate.Should().BeFalse();
        warning.Should().NotBeNull();
    }

    [Theory]
    [InlineData("SOUPAPE ")]
    [InlineData(" SOUPAPE")]
    [InlineData("soupape")]
    [InlineData("Soupape")]
    public void Evaluate_WithTrailingSpaceOrCaseVariance_StillMatches(string actualValue)
    {
        // Real fixtures observed a trailing space ("SOUPAPE ") -- spec §7 requires trim + case-insensitive comparison.
        var rule = new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : CONSTAT ENCRASSEMENT");
        var extractedFields = new Dictionary<string, string> { ["TypeElement"] = actualValue };

        var (shouldCreate, warning) = _sut.Evaluate([rule], extractedFields);

        shouldCreate.Should().BeTrue();
        warning.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WithRealWorldSpellingVariant_DoesNotMatch()
    {
        // "POINT DE FEU" (real G6306B cell) vs "POINT FEU" (confirmed base value) is a genuine
        // spelling difference, not a casing/whitespace issue -- trim+casing normalization is not
        // meant to paper over this; it's a legitimate non-match, covered by the non-blocking
        // warning policy (spec §6/§7).
        var rule = new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "POINT FEU", "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES");
        var extractedFields = new Dictionary<string, string> { ["TypeElement"] = "POINT DE FEU" };

        var (shouldCreate, warning) = _sut.Evaluate([rule], extractedFields);

        shouldCreate.Should().BeFalse();
        warning.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Evaluate_WithRuleReferencingUnknownField_ThrowsUnknownFieldReferenceException()
    {
        var rule = new ConditionalPointRule("DoesNotExist", ConditionOperator.Equals, "SOUPAPE", "Colonne");

        var act = () => _sut.Evaluate([rule], new Dictionary<string, string>());

        act.Should().Throw<UnknownFieldReferenceException>()
            .Which.FieldName.Should().Be("DoesNotExist");
    }
}
