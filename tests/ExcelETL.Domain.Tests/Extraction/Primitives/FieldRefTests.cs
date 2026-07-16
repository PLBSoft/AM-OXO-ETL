using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Primitives;

public class FieldRefTests
{
    [Fact]
    public void Constructor_WithValidFieldName_CreatesFieldRef()
    {
        var fieldRef = new FieldRef("Identification");

        fieldRef.FieldName.Should().Be("Identification");
        fieldRef.Should().BeAssignableTo<ConcatPart>();
    }

    [Fact]
    public void Constructor_WithSameFieldName_ProducesStructurallyEqualInstances()
    {
        var first = new FieldRef("Identification");
        var second = new FieldRef("Identification");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidFieldName_ThrowsDomainValidationException(string? invalidFieldName)
    {
        var act = () => new FieldRef(invalidFieldName!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("fieldName")
            .Which.ErrorCode.Should().Be(DomainErrorCode.FieldRef_EmptyFieldName);
    }
}
