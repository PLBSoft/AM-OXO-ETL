using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 078.5 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md).
public class ImportProfileDescriptionBuilderPointTests
{
    private const string ClosingSentence =
        "Un élément qui ne remplit aucune de ces conditions est importé normalement, avec un avertissement.";

    private static IReadOnlyList<string> Texts(string sheetName, Func<string, SheetExtractionRule> build) =>
        Describe(Profile([build(sheetName)])).SheetSection(sheetName).Texts();

    [Fact]
    public void Divers_SevenRealRules_BecomeFourGroupedSentences_ThenTheClosingSentence() =>
        Texts("DIVERS", name => Rule(name, pointRules:
        [
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "INSTRUMENTATION", "SYNCHRONISATION INSTRUMENTATION"),
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE (PS941)"),
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : CONSTAT ENCRASSEMENT"),
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS"),
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "POINT DE FEU", "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES"),
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "POINT DE FEU", "PF : VALIDATION CONSTAT ENCRASSEMENT"),
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "POINT DE FEU", "PF : ACCORD TRAVAUX FEU")
        ])).Should().ContainInOrder(
            "Si le type d'élément est « INSTRUMENTATION », l'élément est coché dans la colonne « SYNCHRONISATION INSTRUMENTATION ».",
            "Si le type d'élément est « ZERO ENERGIE », l'élément est coché dans la colonne « ZÉRO ENERGIE EN PRESENCE EE (PS941) ».",
            "Si le type d'élément est « SOUPAPE », l'élément est coché dans les 2 colonnes « SOUPAPE : CONSTAT ENCRASSEMENT », " +
            "« SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS ».",
            "Si le type d'élément est « POINT DE FEU », l'élément est coché dans les 3 colonnes « PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES », " +
            "« PF : VALIDATION CONSTAT ENCRASSEMENT », « PF : ACCORD TRAVAUX FEU ».",
            ClosingSentence);

    [Fact]
    public void AutresJointsTouches_UnconditionalColonnes_ThenNotEquals_ThenClosing() =>
        Texts("AUTRES JOINTS TOUCHES", name => Rule(name,
            unconditionalColonneNames: ["RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "CONTRÔLE ETANCHÉITÉS"],
            pointRules: [new ConditionalPointRule("TypeElement", ConditionOperator.NotEquals, "TUBING", "POSE ÉTIQUETTES")]))
            .Should().ContainInOrder(
                "Chaque élément est coché dans les 2 colonnes « RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS », « CONTRÔLE ETANCHÉITÉS ».",
                "Si le type d'élément n'est pas « TUBING », l'élément est coché dans la colonne « POSE ÉTIQUETTES ».",
                ClosingSentence);

    [Fact]
    public void OneUnconditionalColonne_UsesTheSingular() =>
        Texts("ISOLEMENT", name => Rule(name, unconditionalColonneNames: ["PROLOCK VANNES"]))
            .Should().Contain("Chaque élément est coché dans la colonne « PROLOCK VANNES ».");

    [Fact]
    public void Isolement_ZeroEnergieRule_IsMergedWithTheExpectedValueAndTheCell() =>
        Texts("ISOLEMENT", name => Rule(name,
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("HasZeroEnergie", "V", -1, 0)],
            pointRules: [new ConditionalPointRule("HasZeroEnergie", ConditionOperator.Equals, "true", "ZÉRO ENERGIE EN PRESENCE EE (PS941)")],
            zeroEnergieExpectedValue: "ZERO ENERGIE"))
            .Should().ContainInOrder(
                "Si l'indicateur zéro énergie contient « ZERO ENERGIE », l'élément est coché dans la colonne " +
                "« ZÉRO ENERGIE EN PRESENCE EE (PS941) ». Toute autre valeur non vide donne un avertissement.",
                ClosingSentence);

    [Fact]
    public void Isolement_ZeroEnergieRule_WithoutExpectedValue_SaysTheColonneIsNeverTicked() =>
        Texts("ISOLEMENT", name => Rule(name,
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("HasZeroEnergie", "V", -1, 0)],
            pointRules: [new ConditionalPointRule("HasZeroEnergie", ConditionOperator.Equals, "true", "ZÉRO ENERGIE EN PRESENCE EE (PS941)")]))
            .Should().Contain(
                "L'indicateur zéro énergie n'est jamais évalué (valeur attendue non renseignée ou cellule absente du bloc) : " +
                "la colonne « ZÉRO ENERGIE EN PRESENCE EE (PS941) » n'est jamais cochée.");

    [Fact]
    public void Platines_FourFieldPresenceRules_BecomeTwoSentencesWithTwoCells_AndNoClosingSentence()
    {
        var texts = Texts("PLATINES", name => Rule(name, firstBlockStartRow: 17, step: 8, fieldPresencePointRules:
        [
            new FieldPresencePointRule(new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "RECEPTION DEBUT MAD", "DEBUT MAD"),
            new FieldPresencePointRule(new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3), "RECEPTION DEBUT MAD", "DEBUT MAD"),
            new FieldPresencePointRule(new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "RECEPTION DEBUT REL", "DEBUT REL"),
            new FieldPresencePointRule(new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3), "RECEPTION DEBUT REL", "DEBUT REL")
        ]));

        texts.Should().ContainInOrder(
            "Si la cellule H19:N19 ou H20:N20 contient « DEBUT MAD », l'élément est coché dans la colonne « RECEPTION DEBUT MAD ».",
            "Si la cellule H19:N19 ou H20:N20 contient « DEBUT REL », l'élément est coché dans la colonne « RECEPTION DEBUT REL ».");
        texts.Should().NotContain(ClosingSentence);
    }

    [Fact]
    public void FieldPresenceRuleWithoutExpectedValue_TicksWhenTheCellIsFilledIn() =>
        Texts("PLATINES", name => Rule(name, firstBlockStartRow: 17, fieldPresencePointRules:
            [new FieldPresencePointRule(new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "RECEPTION DEBUT MAD")]))
            .Should().Contain("Si la cellule H19:N19 est renseignée, l'élément est coché dans la colonne « RECEPTION DEBUT MAD ».");

    [Fact]
    public void GroupingKey_IgnoresCaseAndSurroundingSpaces_LikeTheEngine() =>
        Texts("DIVERS", name => Rule(name, pointRules:
        [
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "A"),
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, " soupape  ", "B")
        ])).Should().Contain("Si le type d'élément est « SOUPAPE », l'élément est coché dans les 2 colonnes « A », « B ».")
            .And.ContainSingle(t => t.StartsWith("Si le type"));

    [Fact]
    public void SameColonneForTwoValues_GivesTwoSentences() =>
        Texts("DIVERS", name => Rule(name, pointRules:
        [
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "A"),
            new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "VANNE", "A")
        ])).Should().ContainInOrder(
            "Si le type d'élément est « SOUPAPE », l'élément est coché dans la colonne « A ».",
            "Si le type d'élément est « VANNE », l'élément est coché dans la colonne « A ».");

    [Fact]
    public void SheetWithoutAnyPointSetting_HasNoPointSentence() =>
        Texts("ISOLEMENT", name => Rule(name)).Should().NotContain(t => t.Contains("coché"));

    [Fact]
    public void MembersTheSheetDoesNotRead_AreNotDescribed() =>
        Texts("PLATINES", name => Rule(name,
            pointRules: [new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "A")])).Should()
            .NotContain(t => t.Contains("coché") || t == ClosingSentence);
}
