using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.Domain.Extraction.Primitives;
using FluentAssertions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 078.7 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md, D3).
public class ImportProfileDescriptionBuilderFixedBehaviorTests
{
    private static ProfileDescriptionSection Section(string sheetName) =>
        Describe(Profile([Rule(sheetName, fields: [new BlockFieldDefinition("Identification", "B:E", 0, 1)])])).SheetSection(sheetName);

    public static TheoryData<string, string[]> FixedSentencesBySheet => new()
    {
        {
            "PROCEDURE",
            [
                "Une date de révision illisible fait refuser le fichier entier.",
                "Un type « MAD » devient « TM_PROC_MAD », un type « REL » devient « TM_PROC_REL ».",
                "Une ligne sans ordre est un titre de section, pas une tâche à réaliser."
            ]
        },
        // Lot 084.6: the element sheets' repère and zone are header fields of the profile, nothing is fixed.
        { "ISOLEMENT", [] },
        { "PLATINES", [] },
        { "ORIFICES CAPACITES", [] },
        { "AUTRES JOINTS TOUCHES", [] },
        { "DIVERS", [] },
    };

    [Theory]
    [MemberData(nameof(FixedSentencesBySheet))]
    public void EachProcessedSheet_ListsItsFixedBehaviors_MarkedAsFixed_AndNothingElseIsFixed(string sheetName, string[] expected)
    {
        var section = Section(sheetName);

        section.Sentences.Where(s => s.IsFixed).Select(s => s.Text).Should().Equal(expected);
    }

    [Fact]
    public void FixedSentences_ComeRightAfterTheBlockSentences()
    {
        var sentences = Section("PROCEDURE").Sentences.ToList();

        sentences.FindIndex(s => s.IsFixed).Should().Be(2);
        sentences[2].Text.Should().Be("Une date de révision illisible fait refuser le fichier entier.");
    }

    [Fact]
    public void UnknownSheet_HasNoFixedBehavior() =>
        (ImportSheetUsage.For("MA FEUILLE")?.FixedBehaviors ?? []).Should().BeEmpty();
}
