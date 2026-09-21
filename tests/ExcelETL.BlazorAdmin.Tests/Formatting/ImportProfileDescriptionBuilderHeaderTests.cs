using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using FluentAssertions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 078.4 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md).
public class ImportProfileDescriptionBuilderHeaderTests
{
    private static SheetExtractionRule ProcedureRule(
        IReadOnlyList<HeaderFieldRule>? extraFields = null, IReadOnlyList<HeaderCompositeRule>? composites = null) =>
        Rule("PROCEDURE", firstBlockStartRow: 9, step: 1, fields: [new BlockFieldDefinition("Action", "C:L", 0, 0)],
            headerFields:
            [
                new HeaderFieldRule("nomMAD", new DirectCell("PROCEDURE", "M2:O2"), stripReperePrefix: true),
                new HeaderFieldRule("revision", new DirectCell("PROCEDURE", "P2:Q2")),
                new HeaderFieldRule("dateRev", new DirectCell("PROCEDURE", "R2:T2"), dateFormat: "dd/MM/yyyy"),
                .. extraFields ?? []
            ],
            headerComposites: composites ?? [new HeaderCompositeRule("Designation", "Rév {revision} du {dateRev}")]);

    [Fact]
    public void Procedure_DescribesItsHeaderFieldsWithOptions_ThenTheDesignationTemplate_BeforeTheBlocks()
    {
        var texts = Describe(Profile([ProcedureRule()], reperePrefix: "OXO-")).SheetSection("PROCEDURE").Texts();

        texts.Take(4).Should().Equal(
            "En-tête : le repère de l'équipement (« nomMAD ») est lu en M2:O2, préfixe « OXO- » retiré.",
            "En-tête : la révision (« revision ») est lue en P2:Q2.",
            "En-tête : la date de révision (« dateRev ») est lue en R2:T2, au format « dd/MM/yyyy ».",
            "La désignation de l'équipement suit le modèle « Rév {revision} du {dateRev} », où {revision} et {dateRev} " +
            "sont remplacés par les valeurs lues ci-dessus.");
        texts[4].Should().StartWith("Une tâche est lue");
    }

    [Fact]
    public void AutresJointsTouches_RepereEcho_ExplainsHowTheElementRepereIsBuilt() =>
        Describe(Profile([Rule("AUTRES JOINTS TOUCHES",
                headerFields: [new HeaderFieldRule("repereEcho", new DirectCell("AUTRES JOINTS TOUCHES", "N6"))])]))
            .SheetSection("AUTRES JOINTS TOUCHES").Texts().Should().Contain(
                "En-tête : le repère de l'équipement (« repereEcho ») est lu en N6. " +
                "Le repère de l'élément est cette valeur, un tiret, puis l'identifiant.");

    [Fact]
    public void FieldReadOnAnotherSheet_NamesThatSheet() =>
        Describe(Profile([Rule("DIVERS",
                headerFields: [new HeaderFieldRule("repereEcho", new DirectCell("PROCEDURE", "M2:O2"))])]))
            .SheetSection("DIVERS").Texts().Should().Contain(
                "En-tête : le repère de l'équipement (« repereEcho ») est lu en M2:O2 de la feuille « PROCEDURE ». " +
                "Le repère de l'élément est cette valeur, un tiret, puis l'identifiant.");

    [Fact]
    public void ExtraFieldReferencedByTheDesignationTemplate_IsDescribed()
    {
        var rule = ProcedureRule(
            extraFields: [new HeaderFieldRule("indice", new DirectCell("PROCEDURE", "U2"))],
            composites: [new HeaderCompositeRule("Designation", "Rév {revision} du {dateRev} indice {indice}")]);

        var texts = Describe(Profile([rule])).SheetSection("PROCEDURE").Texts();

        texts.Should().Contain("En-tête : le champ « indice » est lu en U2.");
        texts.Should().Contain(
            "La désignation de l'équipement suit le modèle « Rév {revision} du {dateRev} indice {indice} », " +
            "où {revision}, {dateRev} et {indice} sont remplacés par les valeurs lues ci-dessus.");
    }

    [Fact]
    public void ExtraFieldNotReferenced_IsNotDescribedAmongTheSentences() =>
        Describe(Profile([ProcedureRule(extraFields: [new HeaderFieldRule("inutile", new DirectCell("PROCEDURE", "A1"))])]))
            .SheetSection("PROCEDURE").Texts().Should().NotContain(t => t.Contains("inutile"));

    [Fact]
    public void DesignationTemplateWithOnePlaceholder_UsesTheSingular() =>
        Describe(Profile([ProcedureRule(composites: [new HeaderCompositeRule("Designation", "Rév {revision}")])]))
            .SheetSection("PROCEDURE").Texts().Should().Contain(
                "La désignation de l'équipement suit le modèle « Rév {revision} », où {revision} est remplacé par la valeur lue ci-dessus.");

    [Fact]
    public void DesignationTemplateWithoutPlaceholder_IsAFixedText() =>
        Describe(Profile([ProcedureRule(composites: [new HeaderCompositeRule("Designation", "Procédure")])]))
            .SheetSection("PROCEDURE").Texts().Should().Contain("La désignation de l'équipement est toujours « Procédure ».");

    // Lot 084.6: every element sheet reads its repère echo from the header (G6).
    [Fact]
    public void ElementSheet_DescribesItsRepereEcho() =>
        Describe(Profile([Rule("ISOLEMENT",
                headerFields: [new HeaderFieldRule("repereEcho", new DirectCell("ISOLEMENT", "K6:T6"))])]))
            .SheetSection("ISOLEMENT").Texts().Should().StartWith(
                "En-tête : le repère de l'équipement (« repereEcho ») est lu en K6:T6. " +
                "Le repère de l'élément est cette valeur, un tiret, puis l'identifiant.");

    // G16: DIVERS' zone is a header field, broadcast on the whole run.
    [Fact]
    public void Divers_DescribesItsZone() =>
        Describe(Profile([Rule("DIVERS",
                headerFields: [new HeaderFieldRule("repereEcho", new DirectCell("DIVERS", "N6")), new HeaderFieldRule("zone", new DirectCell("DIVERS", "B6:E6"))])]))
            .SheetSection("DIVERS").Texts().Should().Contain(
                "En-tête : la zone (« zone ») est lue en B6:E6. Elle est appliquée à l'équipement et à tous les éléments du fichier.");
}
