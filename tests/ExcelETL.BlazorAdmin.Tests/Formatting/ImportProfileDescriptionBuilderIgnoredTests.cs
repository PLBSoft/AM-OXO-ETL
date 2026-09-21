using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static ExcelETL.BlazorAdmin.Tests.Formatting.DescriptionTestSupport;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 078.8 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md, D2).
public class ImportProfileDescriptionBuilderIgnoredTests
{
    private static readonly BlockFieldDefinition[] ProcedureFields =
    [
        new("Action", "C:L", 0, 0), new("Ordre", "B", 0, 0), new("Acteur", "M:N", 0, 0),
        new("Risques", "O:Q", 0, 0), new("TypeTacheMultipleAlias", "R", 0, 0), new("DateValidation", "T:U", 0, 0)
    ];

    private static readonly BlockFieldDefinition[] IsolementFields =
    [
        new("Identification", "B:E", 0, 1), new("Designation", "H:U", -1, 0),
        new("PositionALaPose", "H:O", 1, 2), new("TypeElement", "B:E", 3, 4)
    ];

    private static readonly BlockFieldDefinition[] ElementFields =
    [
        new("Identification", "B:E", 0, 1), new("Designation", "H:V", -1, 0), new("TypeElement", "B:E", 3, 5)
    ];

    private static SheetExtractionRule ProcedureRule(
        IReadOnlyList<string>? unconditionalColonneNames = null,
        IReadOnlyList<HeaderFieldRule>? headerFields = null,
        IReadOnlyList<HeaderCompositeRule>? headerComposites = null) =>
        Rule("PROCEDURE", firstBlockStartRow: 9, step: 1, fields: ProcedureFields,
            unconditionalColonneNames: unconditionalColonneNames,
            headerFields: headerFields ??
            [
                new HeaderFieldRule("nomMAD", "M2:O2", stripReperePrefix: true),
                new HeaderFieldRule("revision", "P2:Q2"),
                new HeaderFieldRule("dateRev", "R2:T2", dateFormat: "dd/MM/yyyy")
            ],
            headerComposites: headerComposites ?? [new HeaderCompositeRule("Designation", "Rév {revision} du {dateRev}")]);

    private static ProfileDescriptionSection Section(SheetExtractionRule rule) =>
        Describe(Profile([rule])).SheetSection(rule.SheetName);

    // Lot 082 (docs/tickets/tickets-tdd-lot-082-points-office-element-parent-procedure.md): the real case
    // of September 16 used to be reported as ignored -- PROCEDURE's unconditional Colonnes now create
    // Points on the Equipement.
    [Fact]
    public void UnconditionalColonneOnProcedure_IsDescribedAsAPointOnTheEquipement_NotIgnored()
    {
        var section = Section(ProcedureRule(unconditionalColonneNames: ["VISITE PRÉALABLE CHANTIER"]));

        section.Ignored.Should().BeEmpty();
        section.Texts().Should().Contain("L'équipement est coché dans la colonne « VISITE PRÉALABLE CHANTIER ».");
    }

    [Fact]
    public void SeveralUnconditionalColonnesOnProcedure_AreDescribedInOneSentence() =>
        Section(ProcedureRule(unconditionalColonneNames: ["A", "B"])).Texts()
            .Should().Contain("L'équipement est coché dans les 2 colonnes « A », « B ».");

    // Lot 083: PROCEDURE's conditional rules tick the Equipement when at least one real task matches,
    // with no "no condition met" warning sentence.
    [Fact]
    public void ConditionalRulesOnProcedure_AreDescribedAsAnyTaskConditions_WithoutTheWarningSentence()
    {
        var baseRule = ProcedureRule();
        var rule = new SheetExtractionRule(
            "PROCEDURE", baseRule.Locator,
            [
                new ConditionalPointRule("TypeTacheMultipleAlias", ConditionOperator.Equals, "MAD", "PROCÉDURE MAD"),
                new ConditionalPointRule("TypeTacheMultipleAlias", ConditionOperator.NotEquals, "REL", "AUTRE")
            ],
            [], baseRule.HeaderFields, baseRule.HeaderComposites);

        var section = Section(rule);

        section.Ignored.Should().BeEmpty();
        section.Texts().Should().Contain(t =>
            t.StartsWith("Si au moins une tâche a ") && t.Contains("« MAD »")
            && t.EndsWith("l'équipement est coché dans la colonne « PROCÉDURE MAD »."));
        section.Texts().Should().Contain(t =>
            t.StartsWith("Si au moins une tâche n'a pas ") && t.EndsWith("l'équipement est coché dans la colonne « AUTRE »."));
        section.Texts().Should().NotContain(t => t.Contains("avertissement"));
    }

    // Client ticket J2M76: since lot 084.6 PLATINES applies conditional rules like every element sheet.
    [Fact]
    public void ConditionalRuleOnPlatines_IsDescribed_NotIgnored()
    {
        var section = Section(Rule("PLATINES", fields: ElementFields,
            pointRules: [new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "A")]));

        section.Ignored.Should().BeEmpty();
        section.Texts().Should().Contain("Si le type d'élément est « SOUPAPE », l'élément est coché dans la colonne « A ».");
    }

    // Since lot 084.6 only PROCEDURE reads no colour.
    [Fact]
    public void ColourSettingsOnProcedure_AreReportedAsIgnored()
    {
        var baseRule = ProcedureRule();
        var rule = Rule("PROCEDURE", firstBlockStartRow: 9, step: 1, fields: ProcedureFields,
            headerFields: baseRule.HeaderFields, headerComposites: baseRule.HeaderComposites,
            defaultCouleurEtiquette: "VERT", allowedCouleursEtiquette: ["ROUGE", "BLANC"]);

        Section(rule).Ignored.Select(i => i.Text).Should().Equal(
            "couleur d'étiquette par défaut « VERT »",
            "couleurs d'étiquette autorisées « ROUGE », « BLANC »");
    }

    // Since lot 084.6 ISOLEMENT reads its repère echo from the header; an unused composite stays ignored.
    [Fact]
    public void UnusedHeaderCompositeOnIsolement_IsReportedAsIgnored() =>
        Section(Rule("ISOLEMENT", fields: IsolementFields,
                headerFields: [new HeaderFieldRule("repereEcho", "N6")],
                headerComposites: [new HeaderCompositeRule("Libelle", "{repereEcho}")]))
            .Ignored.Select(i => i.Text).Should().Equal("modèle d'en-tête « Libelle »");

    [Fact]
    public void UnreferencedExtraHeaderOnProcedure_IsReportedAsIgnored()
    {
        var rule = ProcedureRule(
            headerFields:
            [
                new HeaderFieldRule("nomMAD", "M2:O2", stripReperePrefix: true),
                new HeaderFieldRule("revision", "P2:Q2"),
                new HeaderFieldRule("dateRev", "R2:T2"),
                new HeaderFieldRule("inutile", "A1")
            ],
            headerComposites: [new HeaderCompositeRule("Designation", "Rév {revision} du {dateRev}"), new HeaderCompositeRule("Autre", "x")]);

        Section(rule).Ignored.Select(i => i.Text).Should().Equal("champ d'en-tête « inutile » lu en A1", "modèle d'en-tête « Autre »");
    }

    [Fact]
    public void DefaultColourWithACell_AndAllowedColoursWithoutACell_AreReportedAsUnused()
    {
        Section(Rule("PLATINES", fields: [.. ElementFields, new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1, isRequired: false)],
                headerFields: [new HeaderFieldRule("repereEcho", "K6:U6")],
                defaultCouleurEtiquette: "BLEUE"))
            .Ignored.Select(i => i.Text).Should().Equal("couleur d'étiquette par défaut « BLEUE » (inutilisée : une cellule de couleur est configurée)");

        Section(Rule("AUTRES JOINTS TOUCHES", fields: ElementFields,
                headerFields: [new HeaderFieldRule("repereEcho", "N6")],
                defaultCouleurEtiquette: "BLEUE", allowedCouleursEtiquette: ["ROUGE"]))
            .Ignored.Select(i => i.Text).Should().Equal("couleurs d'étiquette autorisées « ROUGE » (inutilisées : aucune cellule de couleur configurée)");
    }

    [Fact]
    public void UnknownSheetName_GetsASectionWithOnlyAnIgnoredNotice_AfterTheKnownSheets()
    {
        var description = Describe(Profile([Rule("MA FEUILLE", unconditionalColonneNames: ["X"]), Rule("DIVERS", fields: ElementFields,
            headerFields: [new HeaderFieldRule("repereEcho", "N6")])]));

        description.Sections.Select(s => s.Title).Should().EndWith(["Feuille DIVERS", "Feuille MA FEUILLE"]);
        var section = description.SheetSection("MA FEUILLE");
        section.Sentences.Should().BeEmpty();
        section.Ignored.Select(i => i.Text).Should().Equal("Cette feuille n'est pas traitée par l'import (nom non reconnu).");
    }

    [Fact]
    public void SecondRuleWithTheSameSheetName_IsReportedAsNotProcessed()
    {
        var description = Describe(Profile([Rule("DIVERS", fields: ElementFields,
                headerFields: [new HeaderFieldRule("repereEcho", "N6")]),
            Rule("DIVERS", fields: ElementFields)]));

        var sections = description.Sections.Where(s => s.Title == "Feuille DIVERS").ToList();
        sections.Should().HaveCount(2);
        sections[1].Sentences.Should().BeEmpty();
        sections[1].Ignored.Select(i => i.Text).Should().Equal("Cette règle n'est pas traitée : une règle précédente porte déjà ce nom de feuille.");
    }

    [Fact]
    public void MissingRequiredHeaderNames_AreBlocking() =>
        Section(ProcedureRule(headerFields: [new HeaderFieldRule("revision", "P2:Q2"),
                new HeaderFieldRule("dateRev", "R2:T2")],
                headerComposites: [new HeaderCompositeRule("Libelle", "{revision}")]))
            .Blocking.Select(b => b.Text).Should().Equal(
                "Champ d'en-tête « nomMAD » absent : l'extraction de cette feuille échoue.",
                "Modèle d'en-tête « Designation » absent : l'extraction de cette feuille échoue.");

    [Fact]
    public void MissingRequiredBlockField_IsBlocking() =>
        Section(Rule("PLATINES", fields: [new BlockFieldDefinition("Identification", "B:E", 0, 1)]))
            .Blocking.Select(b => b.Text).Should().Equal(
                "Champ d'en-tête « repereEcho » absent : l'extraction de cette feuille échoue.",
                "Champ de bloc « TypeElement » absent : l'extraction de cette feuille échoue.");

    [Fact]
    public void KnownSheetsMissingFromTheProfile_AreBlockingInTheGeneralSection() =>
        Describe(Profile([ProcedureRule(), Rule("ISOLEMENT", fields: IsolementFields)])).Sections[0].Blocking.Select(b => b.Text).Should().Equal(
            "Feuille « PLATINES » absente du profil : l'import échoue.",
            "Feuille « ORIFICES CAPACITES » absente du profil : l'import échoue.",
            "Feuille « AUTRES JOINTS TOUCHES » absente du profil : l'import échoue.",
            "Feuille « DIVERS » absente du profil : l'import échoue.");

    [Fact]
    public void SeededDefaultProfile_HasNothingIgnoredAndNothingBlocking()
    {
        var description = Describe(LoadSeededDefaultProfile());

        description.Sections.Should().OnlyContain(s => s.Ignored.Count == 0 && s.Blocking.Count == 0);
        description.Sections.Should().HaveCount(7);
    }

    private static ImportProfile LoadSeededDefaultProfile()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileDescriptionBuilderIgnoredTests_" + Guid.NewGuid());
        var importProfileStore = new EfImportProfileStore(dbContextFactory);
        var exportProfileStore = new EfExportProfileStore(dbContextFactory);
        var seeder = new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance);
        seeder.SeedAsync().GetAwaiter().GetResult();
        return importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId).GetAwaiter().GetResult()!;
    }
}
