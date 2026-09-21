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
        Texts("DIVERS", name => Rule(name, warnWhenNoConditionalPoint: true, pointRules:
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
        Texts("AUTRES JOINTS TOUCHES", name => Rule(name, warnWhenNoConditionalPoint: true,
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

    // Lot 084 (G3): the closing sentence follows the sheet's own warning setting.
    [Fact]
    public void WithoutTheWarningSetting_NoClosingSentence() =>
        Texts("PLATINES", name => Rule(name,
            pointRules: [new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "A")]))
            .Should().Contain("Si le type d'élément est « SOUPAPE », l'élément est coché dans la colonne « A ».")
            .And.NotContain(ClosingSentence);

    // Lot 084 (G5): zéro énergie is an ordinary rule on an optional block field.
    [Fact]
    public void Isolement_ZeroEnergieRule_IsAnOrdinaryRuleOnItsField() =>
        Texts("ISOLEMENT", name => Rule(name, warnWhenNoConditionalPoint: true,
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("ZeroEnergie", "V", -1, 0, isRequired: false)],
            pointRules: [new ConditionalPointRule("ZeroEnergie", ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE (PS941)")]))
            .Should().ContainInOrder(
                "Si le champ « ZeroEnergie » est « ZERO ENERGIE », l'élément est coché dans la colonne « ZÉRO ENERGIE EN PRESENCE EE (PS941) ».",
                ClosingSentence);

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

    // Lot 084.6: the "filled cell" rules are read by no sheet any more (removed in 84.8).
    [Fact]
    public void MembersTheSheetDoesNotRead_AreNotDescribed() =>
        Texts("PLATINES", name => Rule(name, firstBlockStartRow: 17,
            fieldPresencePointRules: [new FieldPresencePointRule(new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "RECEPTION DEBUT MAD")]))
            .Should().NotContain(t => t.Contains("coché") || t == ClosingSentence);

    // Lot 084.7 (G2)
    [Fact]
    public void IsNotBlankRule_IsDescribedWithoutAValue() =>
        Texts("PLATINES", name => Rule(name, pointRules:
        [
            new ConditionalPointRule("PoseeLe", ConditionOperator.IsNotBlank, null, "A"),
            new ConditionalPointRule("PoseeLe", ConditionOperator.IsNotBlank, null, "B")
        ])).Should().Contain("Si le champ « PoseeLe » est renseigné, l'élément est coché dans les 2 colonnes « A », « B ».");

    [Fact]
    public void IsNotBlankRule_OnProcedure_TicksTheEquipementWhenAnyTaskHasTheField() =>
        Texts("PROCEDURE", name => Rule(name, firstBlockStartRow: 9, step: 1, stopFieldName: "Action",
            fields: [new BlockFieldDefinition("Action", "C:L", 0, 0), new BlockFieldDefinition("Acteur", "M:N", 0, 0)],
            pointRules: [new ConditionalPointRule("Acteur", ConditionOperator.IsNotBlank, null, "A")]))
            .Should().Contain(t => t.StartsWith("Si au moins une tâche a ") && t.EndsWith(" renseigné, l'équipement est coché dans la colonne « A »."));
}
