using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 073 (docs/tickets/tickets-tdd-lot-073-test-aller-retour-sous-formulaires-editeurs-profil.md):
// guard-rail against defect B, "a field lost when a form rebuilds the domain object" (lot 048.1
// header rules, commit 1736886 FieldPresencePointRules). Every test opens an existing item, re-submits
// it without touching anything, saves the profile, and requires the profile handed to SaveAsync to be
// strictly equivalent to the one loaded. The UI is driven only through its ids and gestures, so these
// tests must stay green, assertions untouched, through the internal rewrite of lots 074+.
public class ImportProfileEditorRoundTripTests : BunitContext
{
    private const string DefaultFixture = "default";
    private const string NullExpectedValueFixture = "field-presence-null-expected-value";

    private readonly Mock<IImportProfileStore> _store = new();
    private ImportProfile? _saved;

    public ImportProfileEditorRoundTripTests()
    {
        _store.Setup(s => s.SaveAsync(It.IsAny<ImportProfile>(), It.IsAny<CancellationToken>()))
            .Callback<ImportProfile, CancellationToken>((profile, _) => _saved = profile)
            .Returns(Task.CompletedTask);
        Services.AddSingleton(_store.Object);
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    // -----------------------------------------------------------------------------------------
    // 073.1 -- every sheet rule, opened then re-submitted unchanged.
    // -----------------------------------------------------------------------------------------
    public static TheoryData<string, int, string> SheetRuleCases()
    {
        var data = new TheoryData<string, int, string>();
        foreach (var fixture in new[] { DefaultFixture, NullExpectedValueFixture })
        {
            var profile = LoadFixture(fixture);
            for (var i = 0; i < profile.SheetRules.Count; i++)
            {
                data.Add(fixture, i, profile.SheetRules[i].SheetName);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(SheetRuleCases))]
    public void SheetRule_OpenedAndResubmittedUnchanged_SavesAnEquivalentProfile(string fixture, int ruleIndex, string sheetName) =>
        WithCulture("en-US", () =>
        {
            var original = LoadFixture(fixture);
            var cut = RenderEditor(original);

            cut.Find($"#modify-sheet-rule-button-{ruleIndex}").Click();
            cut.Find($"#save-sheet-rule-button-{ruleIndex}").Click();
            cut.Find("#save-profile-button").Click();

            AssertSavedEquivalentTo(original, $"{fixture}/{sheetName}");
        });

    // -----------------------------------------------------------------------------------------
    // 073.2 -- every nested item of every sheet rule, opened then re-submitted unchanged.
    // Button ids read from SheetRuleForm.razor ({IdPrefix} = "edit-{i}-" for a rule in edit mode).
    // -----------------------------------------------------------------------------------------
    private static readonly IReadOnlyDictionary<string, (string Modify, string Save)> NestedButtons =
        new Dictionary<string, (string, string)>
        {
            ["block-field"] = ("modify-block-field-button", "save-block-field-button"),
            ["header-field"] = ("modify-header-field-button", "save-header-field-button"),
            ["header-composite"] = ("modify-header-composite-button", "save-header-composite-button"),
            ["field-presence-rule"] = ("modify-field-presence-rule-button", "save-field-presence-rule-button"),
            ["unconditional-colonne"] = ("edit-unconditional-colonne-button", "save-unconditional-colonne-button"),
            ["conditional-point-rule"] = ("edit-conditional-point-rule-button", "save-conditional-point-rule-button"),
        };

    private static IEnumerable<(string SubList, int Count)> NestedCounts(SheetExtractionRule rule) =>
    [
        ("block-field", rule.Locator.Fields.Count),
        ("header-field", rule.HeaderFields.Count),
        ("header-composite", rule.HeaderComposites.Count),
        ("field-presence-rule", rule.FieldPresencePointRules.Count),
        ("unconditional-colonne", rule.UnconditionalColonneNames.Count),
        ("conditional-point-rule", rule.PointRules.Count),
    ];

    public static TheoryData<string, int, string, int, string> NestedItemCases()
    {
        var data = new TheoryData<string, int, string, int, string>();
        foreach (var fixture in new[] { DefaultFixture, NullExpectedValueFixture })
        {
            var profile = LoadFixture(fixture);
            for (var i = 0; i < profile.SheetRules.Count; i++)
            {
                foreach (var (subList, count) in NestedCounts(profile.SheetRules[i]))
                {
                    for (var j = 0; j < count; j++)
                    {
                        data.Add(fixture, i, subList, j, $"{profile.SheetRules[i].SheetName}/{subList}[{j}]");
                    }
                }
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(NestedItemCases))]
    public void NestedItem_OpenedAndResubmittedUnchanged_SavesAnEquivalentProfile(
        string fixture, int ruleIndex, string subList, int itemIndex, string label) =>
        WithCulture("en-US", () =>
        {
            var original = LoadFixture(fixture);
            var cut = RenderEditor(original);
            var prefix = $"edit-{ruleIndex}-";
            var (modify, save) = NestedButtons[subList];

            cut.Find($"#modify-sheet-rule-button-{ruleIndex}").Click();
            cut.Find($"#{prefix}{modify}-{itemIndex}").Click();
            cut.Find($"#{prefix}{save}-{itemIndex}").Click();
            cut.Find($"#save-sheet-rule-button-{ruleIndex}").Click();
            cut.Find("#save-profile-button").Click();

            AssertSavedEquivalentTo(original, $"{fixture}/{label}");
        });

    // -----------------------------------------------------------------------------------------
    // 073.2 -- top-level lists (Tableaux, Applications, TacheMultiple type labels).
    // Button ids read from ImportProfileEditor.razor.
    // -----------------------------------------------------------------------------------------
    private static readonly IReadOnlyDictionary<string, (string Modify, string Save)> TopLevelButtons =
        new Dictionary<string, (string, string)>
        {
            ["default-tableau"] = ("edit-default-tableau-button", "save-default-tableau-button"),
            ["default-application-name"] = ("edit-default-application-name-button", "save-default-application-name-button"),
            ["tache-multiple-type-label"] = ("edit-tache-multiple-type-label-button", "save-tache-multiple-type-label-button"),
        };

    public static TheoryData<string, string, int> TopLevelItemCases()
    {
        var data = new TheoryData<string, string, int>();
        var profile = LoadFixture(DefaultFixture);
        foreach (var (list, count) in new[]
                 {
                     ("default-tableau", profile.DefaultTableaux.Count),
                     ("default-application-name", profile.DefaultApplicationNames.Count),
                     ("tache-multiple-type-label", profile.TacheMultipleTypeLabels.Count),
                 })
        {
            for (var j = 0; j < count; j++)
            {
                data.Add(DefaultFixture, list, j);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(TopLevelItemCases))]
    public void TopLevelItem_OpenedAndResubmittedUnchanged_SavesAnEquivalentProfile(string fixture, string list, int itemIndex) =>
        WithCulture("en-US", () =>
        {
            var original = LoadFixture(fixture);
            var cut = RenderEditor(original);
            var (modify, save) = TopLevelButtons[list];

            cut.Find($"#{modify}-{itemIndex}").Click();
            cut.Find($"#{save}-{itemIndex}").Click();
            cut.Find("#save-profile-button").Click();

            AssertSavedEquivalentTo(original, $"{fixture}/{list}[{itemIndex}]");
        });

    // The default profile's own coverage is guarded too: if a future seeder change dropped every
    // FieldPresencePointRule, the null-ExpectedValue gap (constat 4) would silently widen.
    [Fact]
    public void Fixtures_CoverTheOptionalFieldsTheRoundTripIsMeantToGuard()
    {
        var rules = LoadFixture(DefaultFixture).SheetRules;
        rules.Should().Contain(r => r.HeaderFields.Count > 0 && r.HeaderComposites.Count > 0);
        rules.Should().Contain(r => r.Locator.Fields.Any(f => !f.IsRequired));
        rules.Should().Contain(r => r.WarnWhenNoConditionalPoint);
        rules.Should().Contain(r => r.Locator.Fields.Any(f => f.Name == "CouleurEtiquette") && r.AllowedCouleursEtiquette != null);
        rules.Should().Contain(r => r.DefaultCouleurEtiquette != null);
        // Lot 084.6: the settings the standard profile no longer uses (removed in 84.8) stay guarded by
        // the hand-built fixture until then.
        var handBuilt = LoadFixture(NullExpectedValueFixture).SheetRules;
        handBuilt.Should().Contain(r => r.FieldPresencePointRules.Any(f => f.ExpectedValue == null));
        handBuilt.Should().Contain(r => r.FieldPresencePointRules.Any(f => f.ExpectedValue != null));
        handBuilt.Should().Contain(r => r.ZeroEnergieExpectedValue != null);
        handBuilt.Should().Contain(r => r.CouleurEtiquetteCell != null);
    }

    // -----------------------------------------------------------------------------------------
    // Helpers (local to this file, per the repository's no-shared-test-helper convention).
    // -----------------------------------------------------------------------------------------
    private IRenderedComponent<ImportProfileEditor> RenderEditor(ImportProfile original)
    {
        _store.Setup(s => s.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        return Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, original.Id));
    }

    private void AssertSavedEquivalentTo(ImportProfile original, string because)
    {
        _saved.Should().NotBeNull(because);
        // ImportProfile's Equals is identity-only (Entity), and records compare their list
        // properties by reference unless overridden: compare member by member, in strict order.
        _saved.Should().BeEquivalentTo(original, options => options
            .ComparingByMembers<ImportProfile>()
            .ComparingRecordsByMembers()
            .WithStrictOrdering(), because);
    }

    private static ImportProfile LoadFixture(string fixture) => fixture switch
    {
        DefaultFixture => LoadSeededDefaultProfile(),
        NullExpectedValueFixture => BuildProfileWithNullExpectedValueFieldPresenceRule(),
        _ => throw new ArgumentOutOfRangeException(nameof(fixture), fixture, null),
    };

    private static ImportProfile LoadSeededDefaultProfile()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorRoundTripTests_" + Guid.NewGuid());
        var importProfileStore = new EfImportProfileStore(dbContextFactory);
        var exportProfileStore = new EfExportProfileStore(dbContextFactory);
        var seeder = new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance);
        seeder.SeedAsync().GetAwaiter().GetResult();
        return importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId).GetAwaiter().GetResult()!;
    }

    // Constat 4: the default profile no longer carries a FieldPresencePointRule without ExpectedValue.
    private static ImportProfile BuildProfileWithNullExpectedValueFieldPresenceRule()
    {
        var locator = new RepeatingBlockLocator(
            "PLATINES", firstBlockStartRow: 17, step: 8, stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);
        var rule = new SheetExtractionRule(
            "PLATINES", locator, pointRules: [], unconditionalColonneNames: ["POSE PLATINES"], [], [],
            zeroEnergieExpectedValue: "ZERO ENERGIE",
            fieldPresencePointRules:
            [
                new FieldPresencePointRule(new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "RECEPTION DEBUT MAD"),
                new FieldPresencePointRule(new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3), "RECEPTION DEBUT REL", "DEBUT REL")
            ],
            couleurEtiquetteCell: new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1));
        return new ImportProfile(
            Guid.NewGuid(), "Round trip null ExpectedValue", ImportProfile.DefaultReperePrefix, "MAD TRAVAUX",
            ["TRAVAUX COMPLET"], ["PROGRESS"], [rule]);
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }
}
