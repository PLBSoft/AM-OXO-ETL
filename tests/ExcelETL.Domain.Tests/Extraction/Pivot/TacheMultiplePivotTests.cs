using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Pivot;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Extraction.Pivot;

public class TacheMultiplePivotTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesRealTacheMultiple()
    {
        var tache = new TacheMultiplePivot(
            1, "Consignation vanne principale", "Chef de chantier", "Risque électrique", "TM_PROC_MAD",
            new DateOnly(2026, 1, 15), estFactice: false);

        tache.Ordre.Should().Be(1);
        tache.Action.Should().Be("Consignation vanne principale");
        tache.Acteur.Should().Be("Chef de chantier");
        tache.Risques.Should().Be("Risque électrique");
        tache.TypeTacheMultipleCode.Should().Be("TM_PROC_MAD");
        tache.DateValidation.Should().Be(new DateOnly(2026, 1, 15));
        tache.EstFactice.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNoOrdreAndBlankOptionalFields_CreatesFacticeTacheMultiple()
    {
        var tache = new TacheMultiplePivot(
            null, "--- Présentation ---", string.Empty, string.Empty, string.Empty, null, estFactice: true);

        tache.Ordre.Should().BeNull();
        tache.Action.Should().Be("--- Présentation ---");
        tache.Acteur.Should().BeEmpty();
        tache.Risques.Should().BeEmpty();
        tache.TypeTacheMultipleCode.Should().BeEmpty();
        tache.DateValidation.Should().BeNull();
        tache.EstFactice.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithSameArguments_ProducesStructurallyEqualInstances()
    {
        var first = new TacheMultiplePivot(1, "Action", "Acteur", "Risques", "TM_PROC_MAD", new DateOnly(2026, 1, 15), false);
        var second = new TacheMultiplePivot(1, "Action", "Acteur", "Risques", "TM_PROC_MAD", new DateOnly(2026, 1, 15), false);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidAction_ThrowsDomainValidationException(string? invalidAction)
    {
        var act = () => new TacheMultiplePivot(1, invalidAction!, "Acteur", "Risques", "TM_PROC_MAD", null, false);

        act.Should().Throw<DomainValidationException>()
            .WithParameterName("action")
            .Which.ErrorCode.Should().Be(DomainErrorCode.TacheMultiplePivot_EmptyAction);
    }

    // Lot 067 (docs/tickets/tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md):
    // Repere/TypeElementNom/ColonneTravaux are known only after construction (broadcast by
    // ImportPipelineOrchestrator), same mechanism as IsolementPivot's own broadcast properties.
    [Fact]
    public void Constructor_DefaultsRepereTypeElementNomAndColonneTravaux_ToEmptyString()
    {
        var tache = new TacheMultiplePivot(1, "Action", "Acteur", "Risques", "TM_PROC_MAD", null, false);

        tache.Repere.Should().BeEmpty();
        tache.TypeElementNom.Should().BeEmpty();
        tache.ColonneTravaux.Should().BeEmpty();
    }

    [Fact]
    public void With_SetsRepereTypeElementNomAndColonneTravaux_WithoutAffectingOtherFields()
    {
        var tache = new TacheMultiplePivot(1, "Action", "Acteur", "Risques", "TM_PROC_MAD", null, false);

        var broadcast = tache with { Repere = "38-C7401", TypeElementNom = "MAD TRAVAUX", ColonneTravaux = "Procédure MAD" };

        broadcast.Repere.Should().Be("38-C7401");
        broadcast.TypeElementNom.Should().Be("MAD TRAVAUX");
        broadcast.ColonneTravaux.Should().Be("Procédure MAD");
        broadcast.Action.Should().Be("Action");
        broadcast.TypeTacheMultipleCode.Should().Be("TM_PROC_MAD");
    }
}
