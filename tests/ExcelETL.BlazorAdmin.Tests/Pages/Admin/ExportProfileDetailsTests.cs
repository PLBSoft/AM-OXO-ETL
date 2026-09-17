using System.Globalization;
using Bunit;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests.Layout;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 079.7 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md).
public class ExportProfileDetailsTests : BunitContext
{
    public ExportProfileDetailsTests()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileDetailsTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();
    }

    private IExportProfileStore Store => Services.GetRequiredService<IExportProfileStore>();

    private static void WithFrenchCulture(Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    // One equipement sheet with a fixed-row sentence and a constant column, and a task rule.
    private static ExportProfile BuildProfile(string parentsName = "Parents") => new("Profil d'export détaillé",
    [
        new SheetGenerationRule(parentsName, PivotSource.Equipement,
            [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)], [], [], [new ConstantColumnDefinition("SUPPRESSION", "N")]),
        new SheetGenerationRule("Tâches multiples", PivotSource.TacheMultiple,
            [new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre)], [], [])
    ]);

    private IRenderedComponent<ExportProfileDetails> RenderDetails(Guid id) =>
        Render<ExportProfileDetails>(parameters => parameters.Add(p => p.Id, id));

    private ExportProfile Saved(ExportProfile profile)
    {
        Store.SaveAsync(profile).GetAwaiter().GetResult();
        return profile;
    }

    [Fact]
    public void RendersTheProfileNameAsTitle_AndOneSectionPerDescriptionSection() => WithFrenchCulture(() =>
    {
        var profile = Saved(BuildProfile());

        var cut = RenderDetails(profile.Id);

        cut.Find("h1").TextContent.Should().Be("Détails du profil « Profil d'export détaillé »");
        cut.Find("#details-section-general h2").TextContent.Should().Be("Classeur généré");
        cut.Find("#details-section-sheet-0 h2").TextContent.Should().Be("Feuille Parents");
        cut.Find("#details-section-sheet-1 h2").TextContent.Should().Be("Feuilles par type de tâche (règle « Tâches multiples »)");
    });

    [Fact]
    public void ColumnLetter_IsRenderedInCode_AndHeaderInStrong() => WithFrenchCulture(() =>
    {
        var profile = Saved(BuildProfile());

        var cut = RenderDetails(profile.Id);

        var sentence = cut.FindAll("#details-section-sheet-0 li").Single(li => li.TextContent.StartsWith("Colonne A"));
        sentence.QuerySelector("code.profile-details-cell")!.TextContent.Should().Be("A");
        sentence.QuerySelector("strong.profile-details-value")!.TextContent.Should().Be("Repère");
    });

    [Fact]
    public void FixedSentence_CarriesTheNonEditableBadge_ColumnSentencesDoNot() => WithFrenchCulture(() =>
    {
        var profile = Saved(BuildProfile());

        var cut = RenderDetails(profile.Id);

        var items = cut.FindAll("#details-section-sheet-0 .profile-details-sentences > li");
        var fixedItem = items.Single(li => li.TextContent.StartsWith("Une seule ligne"));
        fixedItem.QuerySelector(".badge")!.TextContent.Should().Be("non modifiable");
        items.Where(li => li != fixedItem).Should().OnlyContain(li => li.QuerySelector(".badge") == null);
    });

    [Fact]
    public void BlockingProblem_IsShownUnderTheGenerationHeading_AndNoIgnoredBlockExists() => WithFrenchCulture(() =>
    {
        // Lot 080.2: an invalid sheet name can no longer be built, the remaining blocking case is a sheet named
        // like a known task code next to a TacheMultiple rule.
        var profile = Saved(BuildProfile("TM_PROC_MAD"));

        var cut = RenderDetails(profile.Id);

        var blocking = cut.Find("#details-section-general-blocking");
        blocking.ClassList.Should().Contain(["alert", "alert-danger"]);
        blocking.TextContent.Should().Contain("Problèmes qui empêchent la génération :").And.Contain("porte le nom d'une feuille de tâches");
        cut.FindAll("[id$='-ignored']").Should().BeEmpty();
    });

    [Fact]
    public void ValidProfile_HasNoBlockingBlock() => WithFrenchCulture(() =>
    {
        var profile = Saved(BuildProfile());

        RenderDetails(profile.Id).FindAll("[id$='-blocking']").Should().BeEmpty();
    });

    [Fact]
    public void UnknownProfileId_ShowsNotFound_AndNoSection() => WithFrenchCulture(() =>
    {
        var cut = RenderDetails(Guid.NewGuid());

        var alert = cut.Find("#export-profile-details-not-found");
        alert.ClassList.Should().Contain(["alert", "alert-danger"]);
        alert.GetAttribute("role").Should().Be("alert");
        alert.TextContent.Should().Be("Profil d'export introuvable.");
        cut.FindAll("section").Should().BeEmpty();
    });

    [Fact]
    public void PageIsReadOnly_NoControlBesidesTheBackLink() => WithFrenchCulture(() =>
    {
        var profile = Saved(BuildProfile());

        RenderDetails(profile.Id).FindAll("button, input, select, textarea, form").Should().BeEmpty();
    });

    [Fact]
    public void Headings_HaveNoLevelSkip() => WithFrenchCulture(() =>
    {
        var profile = Saved(BuildProfile());

        HeadingHierarchyAssertions.AssertNoHeadingLevelSkip(RenderDetails(profile.Id));
    });

    [Fact]
    public void Container_MatchesTheImportDetailsPage() => WithFrenchCulture(() =>
    {
        var profile = Saved(BuildProfile());

        RenderDetails(profile.Id).Find("h1").ParentElement!.GetAttribute("class")
            .Should().Be("container-fluid px-3 profile-editor-container");
    });

    [Fact]
    public void BackToListButton_NavigatesToTheExportProfileList() => WithFrenchCulture(() =>
    {
        var profile = Saved(BuildProfile());
        var cut = Render<SectionOutletTestHost>(parameters => parameters.Add(
            p => p.ChildContent,
            (RenderFragment)(b =>
            {
                b.OpenComponent<ExportProfileDetails>(0);
                b.AddComponentParameter(1, nameof(ExportProfileDetails.Id), profile.Id);
                b.CloseComponent();
            })));

        cut.Find("#back-to-export-profiles-button").Click();

        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/export-profiles");
    });
}
