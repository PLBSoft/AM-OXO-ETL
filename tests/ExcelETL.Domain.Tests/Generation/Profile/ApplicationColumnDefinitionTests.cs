using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Generation.Profile;

public class ApplicationColumnDefinitionTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesApplicationColumnDefinition()
    {
        var column = new ApplicationColumnDefinition("PROGRESS", "PROGRESS", "O");

        column.ApplicationNom.Should().Be("PROGRESS");
        column.Header.Should().Be("PROGRESS");
        column.MarkValue.Should().Be("O");
    }

    [Fact]
    public void Constructor_WithoutMarkValue_DefaultsToX()
    {
        var column = new ApplicationColumnDefinition("PROGRESS", "PROGRESS");

        column.MarkValue.Should().Be("X");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidApplicationNom_ThrowsDomainValidationException(string? invalidApplicationNom)
    {
        var act = () => new ApplicationColumnDefinition(invalidApplicationNom!, "PROGRESS");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("applicationNom")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ApplicationColumnDefinition_EmptyApplicationNom);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidHeader_ThrowsDomainValidationException(string? invalidHeader)
    {
        var act = () => new ApplicationColumnDefinition("PROGRESS", invalidHeader!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("header")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ApplicationColumnDefinition_EmptyHeader);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidMarkValue_ThrowsDomainValidationException(string? invalidMarkValue)
    {
        var act = () => new ApplicationColumnDefinition("PROGRESS", "PROGRESS", invalidMarkValue!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("markValue")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ApplicationColumnDefinition_EmptyMarkValue);
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        var first = new ApplicationColumnDefinition("PROGRESS", "PROGRESS", "O");
        var second = new ApplicationColumnDefinition("PROGRESS", "PROGRESS", "O");

        first.Should().Be(second);
    }
}
