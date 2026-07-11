using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Bunit;
using ExcelETL.BlazorAdmin.Components.Pages;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages;

public class ErrorTests : BunitContext
{
    public ErrorTests() => Services.AddLocalization();

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
    public void Error_WithEnglishCulture_RendersEnglishText() => WithCulture("en-US", () =>
    {
        var cut = Render<Error>();

        cut.Markup.Should().Contain("Error.");
        cut.Markup.Should().Contain("An error occurred while processing your request.");
        cut.Markup.Should().Contain("Development Mode");
    });

    [Fact]
    public void Error_WithFrenchCulture_RendersFrenchText() => WithCulture("fr-FR", () =>
    {
        var cut = Render<Error>();

        cut.Markup.Should().Contain("Erreur.");
        cut.Markup.Should().Contain("Une erreur s'est produite lors du traitement de votre requête.");
        cut.Markup.Should().Contain("Mode Développement");
    });
}
