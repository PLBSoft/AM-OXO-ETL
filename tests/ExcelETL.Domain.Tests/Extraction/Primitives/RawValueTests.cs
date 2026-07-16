using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Primitives;

public class RawValueTests
{
    [Fact]
    public void Instances_AreStructurallyEqual()
    {
        var first = new RawValue();
        var second = new RawValue();

        first.Should().Be(second);
        first.Should().BeAssignableTo<TextTransform>();
    }
}
