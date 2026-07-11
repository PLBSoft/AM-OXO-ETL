using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Bunit;
using ExcelETL.BlazorAdmin.Components.Layout;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Layout;

public class MainLayoutTests : BunitContext
{
    public MainLayoutTests()
    {
        Services.AddLocalization();
        this.AddAuthorization().SetNotAuthorized();
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void MainLayout_WithEnglishCulture_RendersEnglishChrome() => WithCulture("en-US", () =>
    {
        var cut = Render<MainLayout>();

        cut.Markup.Should().Contain("About");
        cut.Markup.Should().Contain("An unhandled error has occurred.");
        cut.Markup.Should().Contain("Reload");
    });

    [Fact]
    public void MainLayout_WithFrenchCulture_RendersFrenchChrome() => WithCulture("fr-FR", () =>
    {
        var cut = Render<MainLayout>();

        cut.Markup.Should().Contain("À propos");
        cut.Markup.Should().Contain("Une erreur non gérée s'est produite.");
        cut.Markup.Should().Contain("Recharger");
    });
}
