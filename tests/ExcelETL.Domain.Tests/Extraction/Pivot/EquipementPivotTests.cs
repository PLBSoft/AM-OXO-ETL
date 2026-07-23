using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Pivot;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Pivot;

public class EquipementPivotTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesEquipementPivot()
    {
        var equipement = new EquipementPivot("C7401", "Rév 1 du 01/01/2026", "MAD TRAVAUX");

        equipement.Repere.Should().Be("C7401");
        equipement.Designation.Should().Be("Rév 1 du 01/01/2026");
        equipement.TypeElementNom.Should().Be("MAD TRAVAUX");
        equipement.Localisation.Should().BeEmpty();
        equipement.Tableaux.Should().BeEmpty();
        equipement.Applications.Should().BeEmpty();
    }

    [Fact]
    public void WithExpression_CanBroadcastLocalisation()
    {
        var equipement = new EquipementPivot("C7401", "Rév 1 du 01/01/2026", "MAD TRAVAUX");

        var broadcast = equipement with { Localisation = "Zone A" };

        broadcast.Localisation.Should().Be("Zone A");
        broadcast.Repere.Should().Be(equipement.Repere);
    }

    [Fact]
    public void WithExpression_CanBroadcastTableauxAndApplications()
    {
        var equipement = new EquipementPivot("C7401", "Rév 1 du 01/01/2026", "MAD TRAVAUX");

        var broadcast = equipement with { Tableaux = ["TRAVAUX COMPLET", "TRAVAUX DETAIL"], Applications = ["PROGRESS"] };

        broadcast.Tableaux.Should().BeEquivalentTo(["TRAVAUX COMPLET", "TRAVAUX DETAIL"], o => o.WithStrictOrdering());
        broadcast.Applications.Should().BeEquivalentTo(["PROGRESS"]);
        broadcast.Repere.Should().Be(equipement.Repere);
    }

    [Fact]
    public void Constructor_WithSameArguments_ProducesStructurallyEqualInstances()
    {
        var first = new EquipementPivot("C7401", "Rév 1 du 01/01/2026", "MAD TRAVAUX");
        var second = new EquipementPivot("C7401", "Rév 1 du 01/01/2026", "MAD TRAVAUX");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void WithExpression_SameTableauxAndApplicationsContent_ProducesStructurallyEqualInstances()
    {
        var first = new EquipementPivot("C7401", "Rév 1 du 01/01/2026", "MAD TRAVAUX")
            with { Tableaux = ["TRAVAUX COMPLET"], Applications = ["PROGRESS"] };
        var second = new EquipementPivot("C7401", "Rév 1 du 01/01/2026", "MAD TRAVAUX")
            with { Tableaux = ["TRAVAUX COMPLET"], Applications = ["PROGRESS"] };

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidRepere_ThrowsDomainValidationException(string? invalidRepere)
    {
        var act = () => new EquipementPivot(invalidRepere!, "Rév 1 du 01/01/2026", "MAD TRAVAUX");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("repere")
            .Which.ErrorCode.Should().Be(DomainErrorCode.EquipementPivot_EmptyRepere);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidDesignation_ThrowsDomainValidationException(string? invalidDesignation)
    {
        var act = () => new EquipementPivot("C7401", invalidDesignation!, "MAD TRAVAUX");

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("designation")
            .Which.ErrorCode.Should().Be(DomainErrorCode.EquipementPivot_EmptyDesignation);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidTypeElementNom_ThrowsDomainValidationException(string? invalidTypeElementNom)
    {
        var act = () => new EquipementPivot("C7401", "Rév 1 du 01/01/2026", invalidTypeElementNom!);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("typeElementNom")
            .Which.ErrorCode.Should().Be(DomainErrorCode.EquipementPivot_EmptyTypeElementNom);
    }
}
