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
            new ProfileDescriptionSegment("Une tâche de type « ", ProfileDescriptionSegmentKind.Text),
            new ProfileDescriptionSegment("TM_PROC_MAD", ProfileDescriptionSegmentKind.Value),
            new ProfileDescriptionSegment(" » est écrite dans la colonne travaux « ", ProfileDescriptionSegmentKind.Text),
            new ProfileDescriptionSegment("Procédure MAD", ProfileDescriptionSegmentKind.Value),
            new ProfileDescriptionSegment(" ».", ProfileDescriptionSegmentKind.Text));
        sentence.Text.Should().Be("Une tâche de type « TM_PROC_MAD » est écrite dans la colonne travaux « Procédure MAD ».");
    }

    [Fact]
    public void QuotedValueList_GivesOneValueSegmentPerValue() =>
        Describe(Profile(defaultApplicationNames: ["PROGRESS", "GMAO"])).Sections[0].Sentences
            .Single(s => s.Text.Contains("applications")).Content.Segments.Where(s => s.Kind == ProfileDescriptionSegmentKind.Value).Select(s => s.Text)
            .Should().Equal("PROGRESS", "GMAO");

    [Fact]
    public void IgnoredAndBlockingItems_AlsoCarryValueSegments()
    {
        var section = Describe(Profile([Rule("PLATINES",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1, isRequired: false)],
            defaultCouleurEtiquette: "VERT")]));

        section.SheetSection("PLATINES").Ignored.Single().Segments.Where(s => s.Kind == ProfileDescriptionSegmentKind.Value).Select(s => s.Text)
            .Should().Equal("VERT");
        section.Sections[0].Blocking.Should().Contain(b => b.Segments.Any(s => s.Kind == ProfileDescriptionSegmentKind.Value && s.Text == "PROCEDURE"));
    }

    [Fact]
    public void TextWithoutQuotedValue_IsASingleTextSegment() =>
        Describe(Profile([Rule("ISOLEMENT")])).SheetSection("ISOLEMENT").Sentences[0].Content.Segments
            .Should().ContainSingle().Which.Kind.Should().Be(ProfileDescriptionSegmentKind.Text);

    [Fact]
    public void ValuesContainingGuillemets_AreStillSplitCorrectly() =>
        Describe(Profile(equipementTypeElementNom: "« MAD » TRAVAUX")).Sections[0].Sentences
            .Single(s => s.Text.StartsWith("L'équipement est créé")).Content.Segments.Where(s => s.Kind == ProfileDescriptionSegmentKind.Value).Select(s => s.Text)
            .Should().Equal("« MAD » TRAVAUX");

    // Lot 078.12.3: cell coordinates are their own segment kind, wherever a range is shown.
    [Fact]
    public void HeaderFieldRange_IsACellReferenceSegment()
    {
        var sentence = Describe(Profile([Rule("DIVERS",
                headerFields: [new HeaderFieldRule("repereEcho", new DirectCell("DIVERS", "N6"))])]))
            .SheetSection("DIVERS").Sentences[0];

        sentence.Content.Segments.Take(4).Should().Equal(
            new ProfileDescriptionSegment("En-tête : le repère de l'équipement (« ", ProfileDescriptionSegmentKind.Text),
            new ProfileDescriptionSegment("repereEcho", ProfileDescriptionSegmentKind.Value),
            new ProfileDescriptionSegment(" ») est lu en ", ProfileDescriptionSegmentKind.Text),
            new ProfileDescriptionSegment("N6", ProfileDescriptionSegmentKind.CellReference));
    }

    [Fact]
    public void EveryKindOfRange_IsMarkedAsACellReference()
    {
        var description = Describe(Profile([
            Rule("PLATINES", firstBlockStartRow: 17, step: 8,
                fields:
                [
                    new BlockFieldDefinition("Identification", "B:E", 0, 1),
                    new BlockFieldDefinition("PoseeLe", "H:N", 2, 2, isRequired: false),
                    new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1, isRequired: false)
                ],
                headerFields: [new HeaderFieldRule("repereEcho", new DirectCell("PLATINES", "K6:U6"))],
                pointRules: [new ConditionalPointRule("PoseeLe", ConditionOperator.IsNotBlank, null, "C")]),
            Rule("DIVERS", fields: [new BlockFieldDefinition("Identification", "H:K", 0, 2)],
                headerFields:
                [
                    new HeaderFieldRule("repereEcho", new DirectCell("DIVERS", "N6")), new HeaderFieldRule("zone", new DirectCell("DIVERS", "B6:E6")),
                    new HeaderFieldRule("inutile", new DirectCell("DIVERS", "A1"))
                ])
        ]));

        var cells = description.Sections
            .SelectMany(s => s.Sentences.Select(x => x.Content).Concat(s.Ignored))
            .SelectMany(t => t.Segments)
            .Where(seg => seg.Kind == ProfileDescriptionSegmentKind.CellReference)
            .Select(seg => seg.Text);

        cells.Should().Contain([
            "B17:E18", // block field
            "K6:U6", // header field
            "H19:N19", // optional block field
            "H18:N18", // couleur block field
            "N6", // header field
            "H17:K19", // block field on DIVERS (first block at row 17)
            "B6:E6", // zone header field
            "A1" // ignored header field
        ]);
    }

    [Fact]
    public void NoInternalMarkerLeaksIntoTheText()
    {
        var description = Describe(Profile([Rule("DIVERS",
            pointRules: [new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "A")])]));

        var allTexts = description.Sections.SelectMany(s =>
            s.Sentences.Select(x => x.Text).Concat(s.Ignored.Select(x => x.Text)).Concat(s.Blocking.Select(x => x.Text)));
        allTexts.Should().OnlyContain(t => !t.Any(c => c >= (char)0xE000 && c <= (char)0xF8FF));
    }
}
