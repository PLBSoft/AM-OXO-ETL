using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 078.2 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md).
public class ImportProfileDescriptionBuilderGeneralTests
{
    private static IReadOnlyList<string> GeneralTexts(ImportProfile profile) => Describe(profile).Sections[0].Texts();

    [Fact]
    public void FirstSection_IsTheGeneralSettings_WithNoFixedSentenceAndNothingIgnored()
    {
        var section = Describe(Profile()).Sections[0];

        section.Title.Should().Be("Paramètres généraux");
        section.Sentences.Should().OnlyContain(s => !s.IsFixed);
        section.Ignored.Should().BeEmpty();
    }

    [Fact]
    public void ReperePrefix_IsDescribedWithItsCaseSensitivityAndRejectionRule() =>
        GeneralTexts(Profile(reperePrefix: "MAD-OXO-")).Should().Contain(
            "Le repère de l'équipement est lu dans la feuille PROCEDURE. Il doit commencer par « MAD-OXO- » " +
            "(majuscules comprises), qui est retiré ; sinon le fichier entier est refusé.");

    [Fact]
    public void EquipementTypeElementNom_IsDescribed() =>
        GeneralTexts(Profile(equipementTypeElementNom: "MAD TRAVAUX")).Should().Contain(
            "L'équipement est créé avec le type d'élément « MAD TRAVAUX ».");

    [Fact]
    public void NoDefaultTableau_SaysSo() =>
        GeneralTexts(Profile(defaultTableaux: [])).Should().Contain("L'équipement n'est rattaché à aucun tableau.");

    [Fact]
    public void OneDefaultTableau_UsesTheSingular() =>
        GeneralTexts(Profile(defaultTableaux: ["TRAVAUX COMPLET"])).Should().Contain(
            "L'équipement est coché dans la colonne « TRAVAUX COMPLET ». " +
            "L'équipement et tous ses éléments sont rattachés à ce tableau.");

    [Fact]
    public void SeveralDefaultTableaux_UseThePlural() =>
        GeneralTexts(Profile(defaultTableaux: ["TRAVAUX COMPLET", "TRAVAUX DETAIL", "VISITE PRÉALABLE CHANTIER"])).Should().Contain(
                "L'équipement est coché dans les 3 colonnes « TRAVAUX COMPLET », « TRAVAUX DETAIL », " +
                "« VISITE PRÉALABLE CHANTIER ». L'équipement et tous ses éléments sont rattachés à ces 3 tableaux.");

    [Fact]
    public void NoDefaultApplication_SaysSo() =>
        GeneralTexts(Profile(defaultApplicationNames: [])).Should().Contain(
            "L'équipement et ses éléments ne sont rattachés à aucune application.");

    [Fact]
    public void OneDefaultApplication_UsesTheSingular() =>
        GeneralTexts(Profile(defaultApplicationNames: ["PROGRESS"])).Should().Contain(
            "L'équipement et tous ses éléments sont rattachés à l'application « PROGRESS ».");

    [Fact]
    public void SeveralDefaultApplications_UseThePlural() =>
        GeneralTexts(Profile(defaultApplicationNames: ["PROGRESS", "GMAO"])).Should().Contain(
            "L'équipement et tous ses éléments sont rattachés aux 2 applications « PROGRESS », « GMAO ».");

    [Fact]
    public void EachTacheMultipleTypeLabel_GetsItsOwnSentence_InProfileOrder() =>
        GeneralTexts(Profile(tacheMultipleTypeLabels:
            [new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD"), new TacheMultipleTypeLabel("TM_PROC_REL", "Procédure REL")])).Should().ContainInOrder(
                "Une tâche de type « TM_PROC_MAD » est écrite dans la colonne travaux « Procédure MAD ».",
                "Une tâche de type « TM_PROC_REL » est écrite dans la colonne travaux « Procédure REL ».");

    [Fact]
    public void NoTacheMultipleTypeLabel_AddsNoTacheSentence() =>
        GeneralTexts(Profile(tacheMultipleTypeLabels: [])).Should().NotContain(t => t.Contains("tâche"));

    [Fact]
    public void Sentences_FollowTheCatalogueOrder() =>
        GeneralTexts(Profile(defaultTableaux: ["T"], defaultApplicationNames: ["A"],
            tacheMultipleTypeLabels: [new TacheMultipleTypeLabel("C", "L")])).Should().HaveCount(5)
            .And.SatisfyRespectively(
                t => t.Should().StartWith("Le repère de l'équipement"),
                t => t.Should().StartWith("L'équipement est créé"),
                t => t.Should().StartWith("L'équipement est coché"),
                t => t.Should().Contain("application"),
                t => t.Should().StartWith("Une tâche"));
}
