using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Primitives;

public class ConcatTests
{
    [Fact]
    public void Constructor_WithValidParts_CreatesConcat()
    {
        IReadOnlyList<ConcatPart> parts = [new FieldRef("Repere"), new Literal("-"), new FieldRef("Identification")];

        var concat = new Concat(parts);

        concat.Parts.Should().BeEquivalentTo(parts, options => options.WithStrictOrdering());
        concat.Should().BeAssignableTo<TextTransform>();
    }

    [Fact]
    public void Constructor_WithSamePartsValues_ButDifferentListInstances_ProducesStructurallyEqualInstances()
    {
        var first = new Concat([new FieldRef("Repere"), new Literal("-"), new FieldRef("Identification")]);
        var second = new Concat([new FieldRef("Repere"), new Literal("-"), new FieldRef("Identification")]);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Constructor_WithDifferentParts_ProducesUnequalInstances()
    {
        var first = new Concat([new FieldRef("Repere")]);
        var second = new Concat([new FieldRef("Identification")]);

        first.Should().NotBe(second);
    }

    [Fact]
    public void Constructor_WithNullParts_ThrowsArgumentNullException()
    {
        var act = () => new Concat(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyParts_ThrowsDomainValidationException()
    {
        var act = () => new Concat([]);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("parts")
            .Which.ErrorCode.Should().Be(DomainErrorCode.Concat_EmptyParts);
    }
}
