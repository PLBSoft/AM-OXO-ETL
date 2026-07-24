using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Domain.Tests.Generation.Fields;

public class PivotFieldResolverTests
{
    private static EquipementPivot Equipement() =>
        new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX")
            with { Localisation = "ZONE 1", Tableaux = ["TRAVAUX COMPLET", "TRAVAUX DETAIL"] };

    private static IsolementPivot Isolement() =>
        new IsolementPivot("C7401-V4", "Vanne 4", "VANNE", "MAD", "ZONE 1")
            with { Tableaux = ["TRAVAUX COMPLET", "TRAVAUX DETAIL"], RepereParent = "38-C7401" };

    private static TacheMultiplePivot TacheMultiple() => new(
        ordre: 3, action: "Consigner", acteur: "ADF", risques: "Aucun",
        typeTacheMultipleCode: "TM_PROC_MAD", dateValidation: new DateOnly(2026, 7, 22), estFactice: false);

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
    public void Resolve_EquipementTableaux_ReturnsCommaJoinedTableaux()
    {
        PivotFieldResolver.Resolve(Equipement(), PivotFieldRef.EquipementTableaux).Should().Be("TRAVAUX COMPLET, TRAVAUX DETAIL");
    }

    [Fact]
    public void Resolve_EquipementTableauxWhenEmpty_ReturnsEmptyString()
    {
        var equipement = new EquipementPivot("38-C7401", "Compresseur C7401", "MAD TRAVAUX");

        PivotFieldResolver.Resolve(equipement, PivotFieldRef.EquipementTableaux).Should().BeEmpty();
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
    public void Resolve_IsolementTableaux_ReturnsCommaJoinedTableaux()
    {
        PivotFieldResolver.Resolve(Isolement(), PivotFieldRef.IsolementTableaux).Should().Be("TRAVAUX COMPLET, TRAVAUX DETAIL");
    }

    [Fact]
    public void Resolve_IsolementRepereParent_ReturnsRepereParent()
    {
        PivotFieldResolver.Resolve(Isolement(), PivotFieldRef.IsolementRepereParent).Should().Be("38-C7401");
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
    [InlineData(PivotFieldRef.EquipementTableaux, PivotSource.Equipement)]
    [InlineData(PivotFieldRef.IsolementRepere, PivotSource.Isolement)]
    [InlineData(PivotFieldRef.IsolementDesignation, PivotSource.Isolement)]
    [InlineData(PivotFieldRef.IsolementTypeElementNom, PivotSource.Isolement)]
    [InlineData(PivotFieldRef.IsolementPositionALaPose, PivotSource.Isolement)]
    [InlineData(PivotFieldRef.IsolementLocalisation, PivotSource.Isolement)]
    [InlineData(PivotFieldRef.IsolementTableaux, PivotSource.Isolement)]
    [InlineData(PivotFieldRef.IsolementRepereParent, PivotSource.Isolement)]
    [InlineData(PivotFieldRef.TacheMultipleOrdre, PivotSource.TacheMultiple)]
    [InlineData(PivotFieldRef.TacheMultipleAction, PivotSource.TacheMultiple)]
    [InlineData(PivotFieldRef.TacheMultipleActeur, PivotSource.TacheMultiple)]
    [InlineData(PivotFieldRef.TacheMultipleRisques, PivotSource.TacheMultiple)]
    [InlineData(PivotFieldRef.TacheMultipleDateValidation, PivotSource.TacheMultiple)]
    public void GetPivotSource_ForEveryField_ReturnsExpectedSource(PivotFieldRef fieldRef, PivotSource expected)
    {
        PivotFieldResolver.GetPivotSource(fieldRef).Should().Be(expected);
    }

    [Fact]
    public void Resolve_TacheMultipleOrdre_ReturnsOrdreAsString()
    {
        PivotFieldResolver.Resolve(TacheMultiple(), PivotFieldRef.TacheMultipleOrdre).Should().Be("3");
    }

    [Fact]
    public void Resolve_TacheMultipleOrdreWhenNull_ReturnsEmptyString()
    {
        var tacheMultiple = new TacheMultiplePivot(null, "Consigner", "", "", "TM_PROC_MAD", null, true);

        PivotFieldResolver.Resolve(tacheMultiple, PivotFieldRef.TacheMultipleOrdre).Should().Be(string.Empty);
    }

    [Fact]
    public void Resolve_TacheMultipleAction_ReturnsAction()
    {
        PivotFieldResolver.Resolve(TacheMultiple(), PivotFieldRef.TacheMultipleAction).Should().Be("Consigner");
    }

    [Fact]
    public void Resolve_TacheMultipleActeur_ReturnsActeur()
    {
        PivotFieldResolver.Resolve(TacheMultiple(), PivotFieldRef.TacheMultipleActeur).Should().Be("ADF");
    }

    [Fact]
    public void Resolve_TacheMultipleRisques_ReturnsRisques()
    {
        PivotFieldResolver.Resolve(TacheMultiple(), PivotFieldRef.TacheMultipleRisques).Should().Be("Aucun");
    }

    [Fact]
    public void Resolve_TacheMultipleDateValidation_ReturnsFormattedDate()
    {
        PivotFieldResolver.Resolve(TacheMultiple(), PivotFieldRef.TacheMultipleDateValidation).Should().Be("22/07/2026");
    }

    [Fact]
    public void Resolve_TacheMultipleDateValidationWhenNull_ReturnsEmptyString()
    {
        var tacheMultiple = new TacheMultiplePivot(1, "Consigner", "ADF", "Aucun", "TM_PROC_MAD", null, false);

        PivotFieldResolver.Resolve(tacheMultiple, PivotFieldRef.TacheMultipleDateValidation).Should().Be(string.Empty);
    }

    [Fact]
    public void Resolve_TacheMultiplePivotWithEquipementFieldRef_ThrowsInvalidOperationException()
    {
        var act = () => PivotFieldResolver.Resolve(TacheMultiple(), PivotFieldRef.EquipementRepere);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AllPivotFieldRefValues_AreDistinct()
    {
        var values = Enum.GetValues<PivotFieldRef>();

        values.Should().OnlyHaveUniqueItems();
    }
}
