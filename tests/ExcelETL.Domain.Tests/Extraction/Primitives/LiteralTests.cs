using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Primitives;

public class LiteralTests
{
    [Fact]
    public void Constructor_WithText_CreatesLiteral()
    {
        var literal = new Literal("-");

        literal.Text.Should().Be("-");
        literal.Should().BeAssignableTo<ConcatPart>();
    }

    [Fact]
    public void Constructor_WithSameText_ProducesStructurallyEqualInstances()
    {
        var first = new Literal("-");
        var second = new Literal("-");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
