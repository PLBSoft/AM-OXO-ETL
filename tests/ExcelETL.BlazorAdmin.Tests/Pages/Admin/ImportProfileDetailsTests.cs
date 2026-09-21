using System.Globalization;
using Bunit;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests.Layout;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 078.9 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md).
public class ImportProfileDetailsTests : BunitContext
{
    public ImportProfileDetailsTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileDetailsTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
    }

    private IImportProfileStore Store => Services.GetRequiredService<IImportProfileStore>();

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

    // ISOLEMENT with every required block field (no blocking problem on its own section), a fixed
    // behavior (repère K6:T6) and an ignored setting (a colour on a sheet that reads none).
    private static ImportProfile BuildProfile()
    {
        var isolement = new SheetExtractionRule(
            "ISOLEMENT",
            new RepeatingBlockLocator("ISOLEMENT", 19, 7, "Identification",
            [
                new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("Designation", "H:U", -1, 0),
                new BlockFieldDefinition("PositionALaPose", "H:O", 1, 2), new BlockFieldDefinition("TypeElement", "B:E", 3, 4)
            ]),
            [], ["PROLOCK VANNES"], [], [],
            defaultCouleurEtiquette: "VERT");

        return new ImportProfile("Profil détaillé", "OXO-", "MAD TRAVAUX", [], [], [isolement]);
    }

    private IRenderedComponent<ImportProfileDetails> RenderDetails(Guid id) =>
        Render<ImportProfileDetails>(parameters => parameters.Add(p => p.Id, id));

    [Fact]
    public void RendersTheProfileNameAsTitle_AndOneSectionPerDescriptionSection() => WithFrenchCulture(() =>
    {
        var profile = BuildProfile();
        Store.SaveAsync(profile).GetAwaiter().GetResult();

        var cut = RenderDetails(profile.Id);

        cut.Find("h1").TextContent.Should().Be("Détails du profil d'import « Profil détaillé »");
        cut.Find("#details-section-general h2").TextContent.Should().Be("Paramètres généraux");
        cut.Find("#details-section-sheet-0 h2").TextContent.Should().Be("Feuille ISOLEMENT");
        cut.FindAll("#details-section-sheet-0 li").Select(li => li.TextContent).Should().Contain(
            "Chaque élément est coché dans la colonne « PROLOCK VANNES ».");
    });

    [Fact]
    public void FixedSentence_CarriesTheNonEditableBadge_OtherSentencesDoNot() => WithFrenchCulture(() =>
    {
        var profile = BuildProfile();
        Store.SaveAsync(profile).GetAwaiter().GetResult();

        var cut = RenderDetails(profile.Id);

        var items = cut.FindAll("#details-section-sheet-0 .profile-details-sentences > li");
        var fixedItem = items.Single(li => li.TextContent.StartsWith("Le repère de l'élément est la cellule K6:T6"));
        fixedItem.QuerySelector(".badge")!.TextContent.Should().Be("non modifiable");
        items.Where(li => li != fixedItem).Should().OnlyContain(li => li.QuerySelector(".badge") == null);
    });

    [Fact]
    public void IgnoredAndBlockingAlerts_AreShownOnlyWhereThereIsSomethingToReport() => WithFrenchCulture(() =>
    {
        var profile = BuildProfile();
        Store.SaveAsync(profile).GetAwaiter().GetResult();

        var cut = RenderDetails(profile.Id);

        var ignored = cut.Find("#details-section-sheet-0-ignored");
        ignored.ClassList.Should().Contain(["alert", "alert-warning"]);
        ignored.HasAttribute("role").Should().BeFalse();
        ignored.TextContent.Should().Contain("Configuré mais ignoré pour cette feuille").And.Contain("couleur d'étiquette par défaut « VERT »");
        cut.FindAll("#details-section-sheet-0-blocking").Should().BeEmpty();

        var blocking = cut.Find("#details-section-general-blocking");
        blocking.ClassList.Should().Contain(["alert", "alert-danger"]);
        blocking.TextContent.Should().Contain("Feuille « PROCEDURE » absente du profil : l'import échoue.");
        cut.FindAll("#details-section-general-ignored").Should().BeEmpty();
    });

    // Lot 078.12.2: profile values are emphasised, the guillemets stay outside the emphasis.
    [Fact]
    public void ProfileValues_AreRenderedInStrong_InSentencesAndInIgnoredItems() => WithFrenchCulture(() =>
    {
        var profile = BuildProfile();
        Store.SaveAsync(profile).GetAwaiter().GetResult();

        var cut = RenderDetails(profile.Id);

        var typeSentence = cut.FindAll("#details-section-general li").Single(li => li.TextContent.StartsWith("L'équipement est créé"));
        typeSentence.QuerySelectorAll("strong.profile-details-value").Select(s => s.TextContent).Should().Equal("MAD TRAVAUX");
        typeSentence.InnerHtml.Should().Contain("« <strong class=\"profile-details-value\">MAD TRAVAUX</strong> »");
        cut.Find("#details-section-sheet-0-ignored strong.profile-details-value").TextContent.Should().Be("VERT");
    });

    // Lot 078.12.3: cell coordinates rendered as <code>.
    [Fact]
    public void CellCoordinates_AreRenderedInCode() => WithFrenchCulture(() =>
    {
        var profile = BuildProfile();
        Store.SaveAsync(profile).GetAwaiter().GetResult();

        var cut = RenderDetails(profile.Id);

        var fixedSentence = cut.FindAll("#details-section-sheet-0 li").Single(li => li.TextContent.StartsWith("Le repère de l'élément"));
        fixedSentence.QuerySelectorAll("code.profile-details-cell").Select(c => c.TextContent).Should().Equal("K6:T6");
        fixedSentence.InnerHtml.Should().Contain("la cellule <code class=\"profile-details-cell\">K6:T6</code>, un tiret");
    });

    [Fact]
    public void UnknownProfileId_ShowsNotFound_AndNoSection() => WithFrenchCulture(() =>
    {
        var cut = RenderDetails(Guid.NewGuid());

        cut.Find("#import-profile-details-not-found").ClassList.Should().Contain(["alert", "alert-danger"]);
        cut.FindAll("section").Should().BeEmpty();
        cut.Find("h1").TextContent.Should().Be("Détails du profil d'import");
    });

    [Fact]
    public void PageIsReadOnly_NoControlBesidesTheBackLink() => WithFrenchCulture(() =>
    {
        var profile = BuildProfile();
        Store.SaveAsync(profile).GetAwaiter().GetResult();

        var cut = RenderDetails(profile.Id);

        cut.FindAll("button, input, select, textarea, form").Should().BeEmpty();
    });

    [Fact]
    public void Headings_HaveNoLevelSkip() => WithFrenchCulture(() =>
    {
        var profile = BuildProfile();
        Store.SaveAsync(profile).GetAwaiter().GetResult();

        HeadingHierarchyAssertions.AssertNoHeadingLevelSkip(RenderDetails(profile.Id));
    });

    [Fact]
    public void BackToListButton_NavigatesToTheImportProfileList() => WithFrenchCulture(() =>
    {
        var profile = BuildProfile();
        Store.SaveAsync(profile).GetAwaiter().GetResult();
        var cut = Render<SectionOutletTestHost>(parameters => parameters.Add(
            p => p.ChildContent,
            (RenderFragment)(b =>
            {
                b.OpenComponent<ImportProfileDetails>(0);
                b.AddComponentParameter(1, nameof(ImportProfileDetails.Id), profile.Id);
                b.CloseComponent();
            })));

        cut.Find("#back-to-import-profiles-button").Click();

        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/import-profiles");
    });
}
