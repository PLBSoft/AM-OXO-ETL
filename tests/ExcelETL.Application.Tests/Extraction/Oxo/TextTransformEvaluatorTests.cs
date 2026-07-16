using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction.Oxo;

public class TextTransformEvaluatorTests
{
    private readonly TextTransformEvaluator _sut = new();
    private static readonly IReadOnlyDictionary<string, string> NoExtractedFields = new Dictionary<string, string>();

    [Fact]
    public void Evaluate_RawValue_ReturnsValueUnchanged()
    {
        var (value, error) = _sut.Evaluate(new RawValue(), "hello", NoExtractedFields);

        value.Should().Be("hello");
        error.Should().BeNull();
    }

    [Fact]
    public void Evaluate_RawValue_WithNullRawValue_ReturnsNull()
    {
        var (value, error) = _sut.Evaluate(new RawValue(), null, NoExtractedFields);

        value.Should().BeNull();
        error.Should().BeNull();
    }

    [Fact]
    public void Evaluate_SubstringAfter_WithMatchingPrefix_StripsIt()
    {
        var (value, error) = _sut.Evaluate(new SubstringAfter("MAD-OXO-"), "MAD-OXO-C7401", NoExtractedFields);

        value.Should().Be("C7401");
        error.Should().BeNull();
    }

    [Fact]
    public void Evaluate_SubstringAfter_WithoutMatchingPrefix_ReturnsErrorMessageInsteadOfThrowing()
    {
        var (value, error) = _sut.Evaluate(new SubstringAfter("MAD-OXO-"), "C7401", NoExtractedFields);

        value.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_SubstringAfter_WithNullRawValue_ReturnsErrorMessageInsteadOfThrowing()
    {
        var (value, error) = _sut.Evaluate(new SubstringAfter("MAD-OXO-"), null, NoExtractedFields);

        value.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_Concat_MixesLiteralsAndFieldRefs()
    {
        var transform = new Concat([new FieldRef("Repere"), new Literal("-"), new FieldRef("Identification")]);
        var extractedFields = new Dictionary<string, string> { ["Repere"] = "C7401", ["Identification"] = "ISO1" };

        var (value, error) = _sut.Evaluate(transform, null, extractedFields);

        value.Should().Be("C7401-ISO1");
        error.Should().BeNull();
    }

    [Fact]
    public void Evaluate_Concat_WithOnlyLiterals_ReturnsConcatenatedLiterals()
    {
        var transform = new Concat([new Literal("Rév "), new Literal("1")]);

        var (value, error) = _sut.Evaluate(transform, null, NoExtractedFields);

        value.Should().Be("Rév 1");
        error.Should().BeNull();
    }

    [Fact]
    public void Evaluate_Concat_WithUnknownFieldRef_ThrowsUnknownFieldReferenceException()
    {
        var transform = new Concat([new FieldRef("DoesNotExist")]);

        var act = () => _sut.Evaluate(transform, null, NoExtractedFields);

        act.Should().Throw<UnknownFieldReferenceException>()
            .Which.FieldName.Should().Be("DoesNotExist");
    }
}
