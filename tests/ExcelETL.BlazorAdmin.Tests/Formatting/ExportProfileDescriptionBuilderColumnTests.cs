using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 079.4 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md).
public class ExportProfileDescriptionBuilderColumnTests
{
    private static ProfileDescriptionSection OnlyRuleSection(SheetGenerationRule rule) =>
        Describe(ExportProfile(rule)).Sections[1];

    [Fact]
    public void EquipementColumns_OneSentencePerColumn_WithLetterHeaderAndContent()
    {
        var section = OnlyRuleSection(ExportRule("Parents", PivotSource.Equipement,
            [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere), new ColumnDefinition("LOC2", null)],
            constantColumns: [new ConstantColumnDefinition("SUPPRESSION", "N")]));

        section.Texts().Skip(1).Should().Equal(
            "Colonne A « Repère » : le repère de l'équipement.",
            "Colonne B « LOC2 » : toujours vide.",
            "Colonne C « SUPPRESSION » : toujours « N ».");
    }

    [Fact]
    public void IsolementColumns_DescribeTheElementFields()
    {
        var section = OnlyRuleSection(ExportRule("Enfants", PivotSource.Isolement,
        [
            new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere),
            new ColumnDefinition("ELEMENT PARENT", PivotFieldRef.IsolementRepereParent),
            new ColumnDefinition("ETIQUETTE", PivotFieldRef.IsolementCouleurEtiquette)
        ]));

        section.Texts().Skip(1).Should().Equal(
            "Colonne A « Numéro » : le repère de l'élément.",
            "Colonne B « ELEMENT PARENT » : le repère de l'équipement.",
            "Colonne C « ETIQUETTE » : la couleur d'étiquette (vide si la feuille d'origine n'en fournit pas).");
    }

    [Fact]
    public void TacheMultipleColumns_CritereIsTheOnlyFixedColumnSentence()
    {
        var section = OnlyRuleSection(ExportRule("Tâches multiples", PivotSource.TacheMultiple,
            [new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre), new ColumnDefinition("CRITERE", PivotFieldRef.TacheMultipleCritere)],
            constantColumns: [new ConstantColumnDefinition("SUPPRESSION", "N")]));

        var columnSentences = section.Sentences.Skip(2).ToList();
        columnSentences.Select(s => s.Text).Should().Equal(
            "Colonne A « Ordre » : l'ordre de la tâche (vide pour un titre de section).",
            "Colonne B « CRITERE » : « A faire », ou « Pour info » pour un titre de section.",
            "Colonne C « SUPPRESSION » : toujours « N ».");
        columnSentences.Select(s => s.IsFixed).Should().Equal(false, true, false);
    }

    [Fact]
    public void ColumnSentence_MarksTheLetterAsACellReference_AndTheHeaderAndConstantAsValues()
    {
        var sentence = OnlyRuleSection(ExportRule("Parents", PivotSource.Equipement,
            constantColumns: [new ConstantColumnDefinition("SUPPRESSION", "N")])).Sentences[1];

        sentence.Content.Segments.Should().Equal(
            new ProfileDescriptionSegment("Colonne ", ProfileDescriptionSegmentKind.Text),
            new ProfileDescriptionSegment("A", ProfileDescriptionSegmentKind.CellReference),
            new ProfileDescriptionSegment(" « ", ProfileDescriptionSegmentKind.Text),
            new ProfileDescriptionSegment("SUPPRESSION", ProfileDescriptionSegmentKind.Value),
            new ProfileDescriptionSegment(" » : toujours « ", ProfileDescriptionSegmentKind.Text),
            new ProfileDescriptionSegment("N", ProfileDescriptionSegmentKind.Value),
            new ProfileDescriptionSegment(" ».", ProfileDescriptionSegmentKind.Text));
    }

    [Fact]
    public void Letters_GoBeyondZ()
    {
        var columns = Enumerable.Range(1, 27).Select(i => new ColumnDefinition($"C{i}", null)).ToList();

        OnlyRuleSection(ExportRule("Parents", PivotSource.Equipement, columns)).Texts().Last()
            .Should().Be("Colonne AA « C27 » : toujours vide.");
    }

    // A PivotFieldRef added to the domain without a label key would show the raw key name on the page.
    public static TheoryData<PivotFieldRef> AllPivotFieldRefs() => [.. Enum.GetValues<PivotFieldRef>()];

    [Theory]
    [MemberData(nameof(AllPivotFieldRefs))]
    public void EveryPivotFieldRef_HasALabel(PivotFieldRef fieldRef)
    {
        var rule = ExportRule("Feuille", PivotFieldResolver.GetPivotSource(fieldRef), [new ColumnDefinition("Titre", fieldRef)]);

        var text = OnlyRuleSection(rule).Texts().Last();

        text.Should().StartWith("Colonne A « Titre » : ").And.NotContain("ExportProfileDetails_");
    }
}
