using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Generation.Profile;

public class ColumnDefinitionTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesColumnDefinition()
    {
        var column = new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere);

        column.Header.Should().Be("Repère");
        column.Source.Should().Be(PivotFieldRef.EquipementRepere);
    }

    [Fact]
    public void Constructor_WithNullSource_CreatesColumnDefinitionWithNoException()
    {
        var act = () => new ColumnDefinition("Colonne libre", null);

        act.Should().NotThrow();
        act().Source.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidHeader_ThrowsDomainValidationException(string? invalidHeader)
    {
        var act = () => new ColumnDefinition(invalidHeader!, PivotFieldRef.EquipementRepere);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("header")
            .Which.ErrorCode.Should().Be(DomainErrorCode.ColumnDefinition_EmptyHeader);
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        var first = new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere);
        var second = new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere);

        first.Should().Be(second);
    }

    [Fact]
    public void Equality_WithDifferentSource_AreNotEqual()
    {
        var first = new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere);
        var second = new ColumnDefinition("Repère", PivotFieldRef.EquipementDesignation);

        first.Should().NotBe(second);
    }
}
