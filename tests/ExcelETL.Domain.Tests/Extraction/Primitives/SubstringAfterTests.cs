using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Primitives;

public class SubstringAfterTests
{
    [Fact]
    public void Constructor_WithValidPrefix_CreatesSubstringAfter()
    {
        var transform = new SubstringAfter("MAD-OXO-");

        transform.Prefix.Should().Be("MAD-OXO-");
        transform.Should().BeAssignableTo<TextTransform>();
    }

    [Fact]
    public void Constructor_WithSamePrefix_ProducesStructurallyEqualInstances()
    {
        var first = new SubstringAfter("MAD-OXO-");
        var second = new SubstringAfter("MAD-OXO-");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidPrefix_ThrowsDomainValidationException(string? invalidPrefix)
    {
        var act = () => new SubstringAfter(invalidPrefix!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("prefix")
            .Which.ErrorCode.Should().Be(DomainErrorCode.SubstringAfter_EmptyPrefix);
    }
}
