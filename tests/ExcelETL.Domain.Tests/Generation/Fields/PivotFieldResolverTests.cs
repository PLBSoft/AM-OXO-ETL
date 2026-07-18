using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Generation.Fields;

public class PivotFieldResolverTests
{
    private static EquipementPivot Equipement() =>
        new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX") with { Localisation = "ZONE 1" };

    private static IsolementPivot Isolement() => new("C7401-V4", "Vanne 4", "VANNE", "MAD", "ZONE 1");

    [Fact]
    public void Resolve_EquipementRepere_ReturnsRepere()
    {
        PivotFieldResolver.Resolve(Equipement(), PivotFieldRef.EquipementRepere).Should().Be("38-C7401");
    }

    [Fact]
    public void Resolve_EquipementDesignation_ReturnsDesignation()
    {
        PivotFieldResolver.Resolve(Equipement(), PivotFieldRef.EquipementDesignation).Should().Be("Compresseur C7401");
    }

    [Fact]
    public void Resolve_EquipementTypeElementNom_ReturnsTypeElementNom()
    {
        PivotFieldResolver.Resolve(Equipement(), PivotFieldRef.EquipementTypeElementNom).Should().Be("MAD TRAVAUX");
    }

    [Fact]
    public void Resolve_EquipementLocalisation_ReturnsLocalisation()
    {
        PivotFieldResolver.Resolve(Equipement(), PivotFieldRef.EquipementLocalisation).Should().Be("ZONE 1");
    }

    [Fact]
    public void Resolve_EquipementPivotWithIsolementFieldRef_ThrowsInvalidOperationException()
    {
        var act = () => PivotFieldResolver.Resolve(Equipement(), PivotFieldRef.IsolementRepere);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Resolve_IsolementRepere_ReturnsRepere()
    {
        PivotFieldResolver.Resolve(Isolement(), PivotFieldRef.IsolementRepere).Should().Be("C7401-V4");
    }

    [Fact]
    public void Resolve_IsolementDesignation_ReturnsDesignation()
    {
        PivotFieldResolver.Resolve(Isolement(), PivotFieldRef.IsolementDesignation).Should().Be("Vanne 4");
    }

    [Fact]
    public void Resolve_IsolementTypeElementNom_ReturnsTypeElementNom()
    {
        PivotFieldResolver.Resolve(Isolement(), PivotFieldRef.IsolementTypeElementNom).Should().Be("VANNE");
    }

    [Fact]
    public void Resolve_IsolementPositionALaPose_ReturnsPositionALaPose()
    {
        PivotFieldResolver.Resolve(Isolement(), PivotFieldRef.IsolementPositionALaPose).Should().Be("MAD");
    }

    [Fact]
    public void Resolve_IsolementLocalisation_ReturnsLocalisation()
    {
        PivotFieldResolver.Resolve(Isolement(), PivotFieldRef.IsolementLocalisation).Should().Be("ZONE 1");
    }

    [Fact]
    public void Resolve_IsolementPivotWithEquipementFieldRef_ThrowsInvalidOperationException()
    {
        var act = () => PivotFieldResolver.Resolve(Isolement(), PivotFieldRef.EquipementRepere);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(PivotFieldRef.EquipementRepere, PivotSource.Equipement)]
    [InlineData(PivotFieldRef.EquipementDesignation, PivotSource.Equipement)]
    [InlineData(PivotFieldRef.EquipementTypeElementNom, PivotSource.Equipement)]
    [InlineData(PivotFieldRef.EquipementLocalisation, PivotSource.Equipement)]
    [InlineData(PivotFieldRef.IsolementRepere, PivotSource.Isolement)]
    [InlineData(PivotFieldRef.IsolementDesignation, PivotSource.Isolement)]
    [InlineData(PivotFieldRef.IsolementTypeElementNom, PivotSource.Isolement)]
    [InlineData(PivotFieldRef.IsolementPositionALaPose, PivotSource.Isolement)]
    [InlineData(PivotFieldRef.IsolementLocalisation, PivotSource.Isolement)]
    public void GetPivotSource_ForEveryField_ReturnsExpectedSource(PivotFieldRef fieldRef, PivotSource expected)
    {
        PivotFieldResolver.GetPivotSource(fieldRef).Should().Be(expected);
    }
}
