using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 078.12.2 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md): every
// profile value shown between guillemets is a separate "value" segment, so the page can emphasise it
// without the builder ever producing markup.
public class ImportProfileDescriptionValueSegmentsTests
{
    [Fact]
    public void QuotedProfileValue_IsItsOwnValueSegment_GuillemetsStayInTheSurroundingText()
    {
        var sentence = Describe(Profile(tacheMultipleTypeLabels: [new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD")]))
            .Sections[0].Sentences.Single(s => s.Text.StartsWith("Une tâche"));

        sentence.Content.Segments.Should().Equal(
            new ProfileDescriptionSegment("Une tâche de type « ", false),
            new ProfileDescriptionSegment("TM_PROC_MAD", true),
            new ProfileDescriptionSegment(" » est écrite dans la colonne travaux « ", false),
            new ProfileDescriptionSegment("Procédure MAD", true),
            new ProfileDescriptionSegment(" ».", false));
        sentence.Text.Should().Be("Une tâche de type « TM_PROC_MAD » est écrite dans la colonne travaux « Procédure MAD ».");
    }

    [Fact]
    public void QuotedValueList_GivesOneValueSegmentPerValue() =>
        Describe(Profile(defaultApplicationNames: ["PROGRESS", "GMAO"])).Sections[0].Sentences
            .Single(s => s.Text.Contains("applications")).Content.Segments.Where(s => s.IsValue).Select(s => s.Text)
            .Should().Equal("PROGRESS", "GMAO");

    [Fact]
    public void IgnoredAndBlockingItems_AlsoCarryValueSegments()
    {
        var section = Describe(Profile([Rule("PLATINES", zeroEnergieExpectedValue: "ZERO ENERGIE")]));

        section.SheetSection("PLATINES").Ignored.Single().Segments.Where(s => s.IsValue).Select(s => s.Text)
            .Should().Equal("ZERO ENERGIE");
        section.Sections[0].Blocking.Should().Contain(b => b.Segments.Any(s => s.IsValue && s.Text == "PROCEDURE"));
    }

    [Fact]
    public void TextWithoutQuotedValue_IsASingleTextSegment() =>
        Describe(Profile([Rule("ISOLEMENT")])).SheetSection("ISOLEMENT").Sentences[0].Content.Segments
            .Should().ContainSingle().Which.IsValue.Should().BeFalse();

    [Fact]
    public void ValuesContainingGuillemets_AreStillSplitCorrectly() =>
        Describe(Profile(equipementTypeElementNom: "« MAD » TRAVAUX")).Sections[0].Sentences
            .Single(s => s.Text.StartsWith("L'équipement est créé")).Content.Segments.Where(s => s.IsValue).Select(s => s.Text)
            .Should().Equal("« MAD » TRAVAUX");

    [Fact]
    public void NoInternalMarkerLeaksIntoTheText()
    {
        var description = Describe(Profile([Rule("DIVERS",
            pointRules: [new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "A")])]));

        var allTexts = description.Sections.SelectMany(s =>
            s.Sentences.Select(x => x.Text).Concat(s.Ignored.Select(x => x.Text)).Concat(s.Blocking.Select(x => x.Text)));
        allTexts.Should().OnlyContain(t => !t.Any(c => c >= '' && c <= ''));
    }
}
