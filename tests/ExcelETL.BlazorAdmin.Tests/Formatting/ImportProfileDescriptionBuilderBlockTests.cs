using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 078.3 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md, D1).
public class ImportProfileDescriptionBuilderBlockTests
{
    [Fact]
    public void Isolement_DescribesStepStartRowStopFieldAndFirstBlockRanges()
    {
        var rule = Rule("ISOLEMENT", firstBlockStartRow: 19, step: 7, fields:
        [
            new BlockFieldDefinition("Identification", "B:E", 0, 1),
            new BlockFieldDefinition("Designation", "H:U", -1, 0),
            new BlockFieldDefinition("PositionALaPose", "H:O", 1, 2),
            new BlockFieldDefinition("TypeElement", "B:E", 3, 4),
            new BlockFieldDefinition("ZeroEnergie", "V", -1, 0)
        ]);

        var texts = Describe(Profile([rule])).SheetSection("ISOLEMENT").Texts();

        texts.Should().ContainInOrder(
            "Un élément est lu toutes les 7 lignes à partir de la ligne 19. " +
            "La lecture s'arrête au premier bloc dont l'identifiant est vide.",
            "Pour le premier élément : identifiant en B19:E20, désignation en H18:U19, position à la pose en H20:O21, " +
            "type d'élément en B22:E23, champ « ZeroEnergie » en V18:V19.");
    }

    [Fact]
    public void Procedure_UsesTaskVocabulary_OneLinePerTask_AndSingleCellRanges()
    {
        var rule = Rule("PROCEDURE", firstBlockStartRow: 9, step: 1, stopFieldName: "Action", fields:
        [
            new BlockFieldDefinition("Action", "C:L", 0, 0),
            new BlockFieldDefinition("Ordre", "B", 0, 0),
            new BlockFieldDefinition("Acteur", "M:N", 0, 0),
            new BlockFieldDefinition("Risques", "O:Q", 0, 0),
            new BlockFieldDefinition("TypeTacheMultipleAlias", "R", 0, 0),
            new BlockFieldDefinition("DateValidation", "T:U", 0, 0)
        ]);

        var texts = Describe(Profile([rule])).SheetSection("PROCEDURE").Texts();

        texts.Should().ContainInOrder(
            "Une tâche est lue par ligne à partir de la ligne 9. La lecture s'arrête à la première ligne dont l'action est vide.",
            "Pour la première tâche : action en C9:L9, ordre en B9, acteur en M9:N9, risques en O9:Q9, type en R9, " +
            "date de validation en T9:U9.");
    }

    [Fact]
    public void Divers_StepOfThree() =>
        Describe(Profile([Rule("DIVERS", firstBlockStartRow: 9, step: 3, stopFieldName: "Identification", fields:
            [
                new BlockFieldDefinition("TypeElement", "B:G", 0, 2),
                new BlockFieldDefinition("Identification", "H:K", 0, 2),
                new BlockFieldDefinition("Designation", "L:V", 0, 2)
            ])]))
            .SheetSection("DIVERS").Texts().Should().ContainInOrder(
                "Un élément est lu toutes les 3 lignes à partir de la ligne 9. " +
                "La lecture s'arrête au premier bloc dont l'identifiant est vide.",
                "Pour le premier élément : type d'élément en B9:G11, identifiant en H9:K11, désignation en L9:V11.");

    [Fact]
    public void ElementSheetWithStepOfOne_UsesPerLineWording() =>
        Describe(Profile([Rule("PLATINES", firstBlockStartRow: 5, step: 1)])).SheetSection("PLATINES").Texts()
            .Should().Contain(
                "Un élément est lu par ligne à partir de la ligne 5. La lecture s'arrête à la première ligne dont l'identifiant est vide.");

    [Fact]
    public void TaskSheetWithStepAboveOne_UsesEveryNLinesWording() =>
        Describe(Profile([Rule("PROCEDURE", firstBlockStartRow: 9, step: 2, fields: [new BlockFieldDefinition("Action", "C:L", 0, 0)])]))
            .SheetSection("PROCEDURE").Texts().Should().Contain(
                "Une tâche est lue toutes les 2 lignes à partir de la ligne 9. La lecture s'arrête au premier bloc dont l'action est vide.");

    [Fact]
    public void UnknownFieldName_IsShownQuoted() =>
        Describe(Profile([Rule("ISOLEMENT", firstBlockStartRow: 19, fields:
            [new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("Commentaire", "W", 0, 0)])]))
            .SheetSection("ISOLEMENT").Texts().Should().Contain(
                "Pour le premier élément : identifiant en B19:E20, champ « Commentaire » en W19.");

    [Fact]
    public void UnknownStopFieldName_IsShownQuoted() =>
        Describe(Profile([Rule("DIVERS", firstBlockStartRow: 19, step: 7, fields: [new BlockFieldDefinition("Repere", "B:E", 0, 1)])]))
            .SheetSection("DIVERS").Texts().Should().Contain(
                "Un élément est lu toutes les 7 lignes à partir de la ligne 19. " +
                "La lecture s'arrête au premier bloc dont le champ « Repere » est vide.");

    [Fact]
    public void KnownSheetSections_FollowPipelineOrder_WhateverTheProfileOrder()
    {
        var description = Describe(Profile([Rule("DIVERS"), Rule("PROCEDURE"), Rule("ISOLEMENT")]));

        description.Sections.Select(s => s.Title).Should().Equal(
            "Paramètres généraux", "Feuille PROCEDURE", "Feuille ISOLEMENT", "Feuille DIVERS");
    }

    [Fact]
    public void BlockSentences_AreNotFixed() =>
        Describe(Profile([Rule("ISOLEMENT")])).SheetSection("ISOLEMENT").Sentences.Take(2).Should().OnlyContain(s => !s.IsFixed);

    // Lot 084.7 (G4): an optional element field is marked; PROCEDURE ignores the setting, so it isn't.
    [Fact]
    public void OptionalElementField_IsMarkedOptional() =>
        Describe(Profile([Rule("PLATINES", firstBlockStartRow: 17, step: 8, fields:
            [new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("PoseeLe", "H:N", 2, 2, isRequired: false)])]))
            .SheetSection("PLATINES").Texts().Should().Contain(
                "Pour le premier élément : identifiant en B17:E18, champ « PoseeLe » en H19:N19 (facultatif).");

    [Fact]
    public void OptionalFieldOnProcedure_IsNotMarked() =>
        Describe(Profile([Rule("PROCEDURE", firstBlockStartRow: 9, step: 1, stopFieldName: "Action",
                fields: [new BlockFieldDefinition("Action", "C:L", 0, 0, isRequired: false)])]))
            .SheetSection("PROCEDURE").Texts().Should().NotContain(t => t.Contains("facultatif"));
}
