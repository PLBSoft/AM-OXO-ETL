using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 079.3 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md).
public class ExportProfileDescriptionBuilderSheetTests
{
    private static readonly ColumnDefinition RepereColumn = new("Repère", PivotFieldRef.EquipementRepere);

    [Fact]
    public void WorkbookSection_ListsTheSheetsInRuleOrder_ThenTheFixedHeaderRowSentence_ThenTheImportProfileDependency()
    {
        var description = Describe(ExportProfile(
            ExportRule("Parents", PivotSource.Equipement, [RepereColumn]),
            ExportRule("Enfants", PivotSource.Isolement, [new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere)]),
            ExportRule("Tâches multiples", PivotSource.TacheMultiple, [new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre)])));

        var section = description.Sections[0];
        section.Title.Should().Be("Classeur généré");
        section.Texts().Should().Equal(
            "Le fichier contient, dans cet ordre : la feuille « Parents », la feuille « Enfants » et une feuille par type de tâche (règle « Tâches multiples »).",
            "Chaque feuille commence par une ligne de titres ; les données commencent à la ligne 2.",
            "Les points, applications, tableaux et types de tâches viennent du profil d'import utilisé avec ce profil d'export.");
        section.Sentences.Select(s => s.IsFixed).Should().Equal(false, true, false);
        section.Blocking.Should().BeEmpty();
        section.Ignored.Should().BeEmpty();
    }

    [Fact]
    public void WorkbookSection_WithASingleSheet_UsesTheSingularSentence() =>
        Describe(ExportProfile(ExportRule("Parents", PivotSource.Equipement, [RepereColumn])))
            .Sections[0].Texts()[0].Should().Be("Le fichier contient la feuille « Parents ».");

    [Fact]
    public void RuleSections_FollowTheRuleOrder_WithOneTitlePerPivotSource()
    {
        var description = Describe(ExportProfile(
            ExportRule("Tâches multiples", PivotSource.TacheMultiple, [new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre)]),
            ExportRule("Enfants", PivotSource.Isolement, [new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere)]),
            ExportRule("Parents", PivotSource.Equipement, [RepereColumn])));

        description.Sections.Skip(1).Select(s => s.Title).Should().Equal(
            "Feuilles par type de tâche (règle « Tâches multiples »)", "Feuille Enfants", "Feuille Parents");
    }

    [Fact]
    public void EquipementSection_StartsWithTheFixedSingleRowSentence()
    {
        var section = Describe(ExportProfile(ExportRule("Parents", PivotSource.Equipement, [RepereColumn]))).Sections[1];

        section.Sentences[0].Text.Should().Be("Une seule ligne : l'équipement.");
        section.Sentences[0].IsFixed.Should().BeTrue();
    }

    [Fact]
    public void IsolementSection_StartsWithTheFixedOneRowPerElementSentence()
    {
        var section = Describe(ExportProfile(
            ExportRule("Enfants", PivotSource.Isolement, [new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere)]))).Sections[1];

        section.Sentences[0].Text.Should().Be(
            "Une ligne par élément, dans l'ordre des feuilles ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS.");
        section.Sentences[0].IsFixed.Should().BeTrue();
    }

    [Fact]
    public void TacheMultipleSection_StartsWithTheTwoFixedSheetSentences()
    {
        var section = Describe(ExportProfile(
            ExportRule("Tâches multiples", PivotSource.TacheMultiple, [new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre)]))).Sections[1];

        section.Texts().Take(2).Should().Equal(
            "Une feuille est créée par type de tâche présent dans le fichier importé, nommée d'après le code du type (« TM_PROC_MAD », « TM_PROC_REL »…), par ordre alphabétique. Le nom « Tâches multiples » n'apparaît pas dans le fichier.",
            "Chaque feuille contient une ligne par tâche de ce type, titres de section compris, dans l'ordre de la feuille PROCEDURE.");
        section.Sentences.Take(2).Should().OnlyContain(s => s.IsFixed);
    }

    [Fact]
    public void RuleWithoutAnyColumn_SaysSo()
    {
        var section = Describe(ExportProfile(ExportRule("Parents", PivotSource.Equipement))).Sections[1];

        section.Texts().Should().Equal("Une seule ligne : l'équipement.", "Aucune colonne.");
        section.Sentences[1].IsFixed.Should().BeFalse();
    }
}
