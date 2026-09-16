using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 073.3 -- export-side mirror of ImportProfileEditorRoundTripTests.cs (see that file's header).
// SheetGenerationRuleForm never exposes ConstantColumnDefinitions: the "Taches multiples" rule's
// round trip must still find them intact (commit 0cbac22).
public class ExportProfileEditorRoundTripTests : BunitContext
{
    private readonly Mock<IExportProfileStore> _store = new();
    private ExportProfile? _saved;

    public ExportProfileEditorRoundTripTests()
    {
        _store.Setup(s => s.SaveAsync(It.IsAny<ExportProfile>(), It.IsAny<CancellationToken>()))
            .Callback<ExportProfile, CancellationToken>((profile, _) => _saved = profile)
            .Returns(Task.CompletedTask);
        Services.AddSingleton(_store.Object);
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    // -----------------------------------------------------------------------------------------
    // Every sheet generation rule, opened then re-submitted unchanged.
    // -----------------------------------------------------------------------------------------
    public static TheoryData<int, string> SheetRuleCases()
    {
        var data = new TheoryData<int, string>();
        var profile = LoadSeededDefaultProfile();
        for (var i = 0; i < profile.SheetRules.Count; i++)
        {
            data.Add(i, profile.SheetRules[i].SheetName);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(SheetRuleCases))]
    public void SheetRule_OpenedAndResubmittedUnchanged_SavesAnEquivalentProfile(int ruleIndex, string sheetName) =>
        WithCulture("en-US", () =>
        {
            var original = LoadSeededDefaultProfile();
            var cut = RenderEditor(original);

            cut.Find($"#modify-sheet-generation-rule-button-{ruleIndex}").Click();
            cut.Find($"#save-sheet-generation-rule-button-{ruleIndex}").Click();
            cut.Find("#save-export-profile-button").Click();

            AssertSavedEquivalentTo(original, sheetName);
        });

    // -----------------------------------------------------------------------------------------
    // Every column / Point column / Application column, opened then re-submitted unchanged.
    // Button ids read from SheetGenerationRuleForm.razor ({IdPrefix} = "edit-{i}-").
    // -----------------------------------------------------------------------------------------
    private static readonly IReadOnlyDictionary<string, (string Modify, string Save)> NestedButtons =
        new Dictionary<string, (string, string)>
        {
            ["column-definition"] = ("modify-column-definition-button", "save-column-definition-button"),
            ["point-column-definition"] = ("modify-point-column-definition-button", "save-point-column-definition-button"),
            ["application-column-definition"] = ("modify-application-column-definition-button", "save-application-column-definition-button"),
        };

    public static TheoryData<int, string, int, string> NestedItemCases()
    {
        var data = new TheoryData<int, string, int, string>();
        var profile = LoadSeededDefaultProfile();
        for (var i = 0; i < profile.SheetRules.Count; i++)
        {
            var rule = profile.SheetRules[i];
            foreach (var (subList, count) in new[]
                     {
                         ("column-definition", rule.ColumnDefinitions.Count),
                         ("point-column-definition", rule.PointColumnDefinitions.Count),
                         ("application-column-definition", rule.ApplicationColumnDefinitions.Count),
                     })
            {
                for (var j = 0; j < count; j++)
                {
                    data.Add(i, subList, j, $"{rule.SheetName}/{subList}[{j}]");
                }
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(NestedItemCases))]
    public void NestedItem_OpenedAndResubmittedUnchanged_SavesAnEquivalentProfile(
        int ruleIndex, string subList, int itemIndex, string label) =>
        WithCulture("en-US", () =>
        {
            var original = LoadSeededDefaultProfile();
            var cut = RenderEditor(original);
            var prefix = $"edit-{ruleIndex}-";
            var (modify, save) = NestedButtons[subList];

            cut.Find($"#modify-sheet-generation-rule-button-{ruleIndex}").Click();
            cut.Find($"#{prefix}{modify}-{itemIndex}").Click();
            cut.Find($"#{prefix}{save}-{itemIndex}").Click();
            cut.Find($"#save-sheet-generation-rule-button-{ruleIndex}").Click();
            cut.Find("#save-export-profile-button").Click();

            AssertSavedEquivalentTo(original, label);
        });

    [Fact]
    public void DefaultProfile_CoversTheOptionalFieldsTheRoundTripIsMeantToGuard()
    {
        var rules = LoadSeededDefaultProfile().SheetRules;
        rules.Should().Contain(r => r.ColumnDefinitions.Any(c => c.Source == null));
        rules.Should().Contain(r => r.PointColumnDefinitions.Count > 0);
        rules.Should().Contain(r => r.ApplicationColumnDefinitions.Count > 0);
        rules.Should().Contain(r => r.PivotSource == PivotSource.TacheMultiple && r.ConstantColumnDefinitions.Count > 0);
    }

    // -----------------------------------------------------------------------------------------
    // Helpers (local to this file, per the repository's no-shared-test-helper convention).
    // -----------------------------------------------------------------------------------------
    private IRenderedComponent<ExportProfileEditor> RenderEditor(ExportProfile original)
    {
        _store.Setup(s => s.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        return Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, original.Id));
    }

    private void AssertSavedEquivalentTo(ExportProfile original, string because)
    {
        _saved.Should().NotBeNull(because);
        // Records compare list properties by reference unless overridden: compare member by
        // member, in strict order.
        _saved.Should().BeEquivalentTo(original, options => options
            .ComparingRecordsByMembers()
            .WithStrictOrdering(), because);
    }

    private static ExportProfile LoadSeededDefaultProfile()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileEditorRoundTripTests_" + Guid.NewGuid());
        var importProfileStore = new EfImportProfileStore(dbContextFactory);
        var exportProfileStore = new EfExportProfileStore(dbContextFactory);
        var seeder = new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance);
        seeder.SeedAsync().GetAwaiter().GetResult();
        return exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId).GetAwaiter().GetResult()!;
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }
}
