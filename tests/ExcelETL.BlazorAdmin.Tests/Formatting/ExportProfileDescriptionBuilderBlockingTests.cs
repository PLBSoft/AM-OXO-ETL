using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using FluentAssertions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 079.6 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md).
public class ExportProfileDescriptionBuilderBlockingTests
{
    private static SheetGenerationRule Parents(string name = "Parents") =>
        ExportRule(name, PivotSource.Equipement, [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)]);

    private static SheetGenerationRule Taches(string name = "Tâches multiples") =>
        ExportRule(name, PivotSource.TacheMultiple, [new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre)]);

    private static IEnumerable<string> BlockingTexts(ProfileDescriptionSection section) => section.Blocking.Select(b => b.Text);

    [Theory]
    [InlineData("TM_PROC_MAD")]
    [InlineData("tm_proc_rel")]
    public void SheetNamedLikeAKnownTaskCode_WithATacheMultipleRule_Blocks(string name)
    {
        var description = Describe(ExportProfile(Parents(name), Taches()));

        BlockingTexts(description.Sections[0]).Should().Equal(
            $"La feuille « {name} » porte le nom d'une feuille de tâches : la génération échoue si le fichier contient des tâches de ce type.");
    }

    [Fact]
    public void SheetNamedLikeAKnownTaskCode_WithoutTacheMultipleRule_DoesNotBlock() =>
        Describe(ExportProfile(Parents("TM_PROC_MAD"))).Sections.Should().OnlyContain(s => s.Blocking.Count == 0);

    [Fact]
    public void LeadingAndTrailingSpaces_DoNotBlock() =>
        Describe(ExportProfile(Parents(" Parents "), Parents("Parents"))).Sections.Should().OnlyContain(s => s.Blocking.Count == 0);

    [Fact]
    public void TacheMultipleRuleName_IsNeverCheckedAsASheetName() =>
        Describe(ExportProfile(Taches(new string('T', 40) + ":"))).Sections.Should().OnlyContain(s => s.Blocking.Count == 0);
}
