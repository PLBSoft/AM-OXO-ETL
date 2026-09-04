using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Generation.Profile;

public class ConstantColumnDefinitionTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesConstantColumnDefinition()
    {
        var column = new ConstantColumnDefinition("CRITERE", "A faire");

        column.Header.Should().Be("CRITERE");
        column.Value.Should().Be("A faire");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidHeader_ThrowsDomainValidationException(string? invalidHeader)
    {
        var act = () => new ConstantColumnDefinition(invalidHeader!, "A faire");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("header")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ConstantColumnDefinition_EmptyHeader);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidValue_ThrowsDomainValidationException(string? invalidValue)
    {
        var act = () => new ConstantColumnDefinition("CRITERE", invalidValue!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("value")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ConstantColumnDefinition_EmptyValue);
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        var first = new ConstantColumnDefinition("CRITERE", "A faire");
        var second = new ConstantColumnDefinition("CRITERE", "A faire");

        first.Should().Be(second);
    }
}
