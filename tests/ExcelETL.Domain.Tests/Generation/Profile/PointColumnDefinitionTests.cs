using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Generation.Profile;

public class PointColumnDefinitionTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesPointColumnDefinition()
    {
        var column = new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes", "X");

        column.ColonneNom.Should().Be("PROLOCK VANNES");
        column.Header.Should().Be("Prolock vannes");
        column.MarkValue.Should().Be("X");
    }

    [Fact]
    public void Constructor_WithoutMarkValue_DefaultsToX()
    {
        var column = new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes");

        column.MarkValue.Should().Be("X");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidColonneNom_ThrowsDomainValidationException(string? invalidColonneNom)
    {
        var act = () => new PointColumnDefinition(invalidColonneNom!, "Prolock vannes");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("colonneNom")
            .Which.ErrorCode.Should().Be(DomainErrorCode.PointColumnDefinition_EmptyColonneNom);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidHeader_ThrowsDomainValidationException(string? invalidHeader)
    {
        var act = () => new PointColumnDefinition("PROLOCK VANNES", invalidHeader!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("header")
            .Which.ErrorCode.Should().Be(DomainErrorCode.PointColumnDefinition_EmptyHeader);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidMarkValue_ThrowsDomainValidationException(string? invalidMarkValue)
    {
        var act = () => new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes", invalidMarkValue!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("markValue")
            .Which.ErrorCode.Should().Be(DomainErrorCode.PointColumnDefinition_EmptyMarkValue);
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        var first = new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes");
        var second = new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes");

        first.Should().Be(second);
    }
}
