using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Bunit;
using ExcelETL.BlazorAdmin.Components.Pages;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages;

public class NotFoundTests : BunitContext
{
    public NotFoundTests() => Services.AddLocalization();

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
    public void NotFound_WithEnglishCulture_RendersEnglishText() => WithCulture("en-US", () =>
    {
        var cut = Render<NotFound>();

        cut.Markup.Should().Contain("Not Found");
        cut.Markup.Should().Contain("Sorry, the content you are looking for does not exist.");
    });

    [Fact]
    public void NotFound_WithFrenchCulture_RendersFrenchText() => WithCulture("fr-FR", () =>
    {
        var cut = Render<NotFound>();

        cut.Markup.Should().Contain("Introuvable");
        cut.Markup.Should().Contain("Désolé, le contenu que vous recherchez n'existe pas.");
    });

    // Lot 042 (42.2): the page previously started at h3 with no h1 at all -- fixed to a proper,
    // unique page-title h1.
    [Fact]
    public void NotFound_HasExactlyOneH1Heading() => WithCulture("en-US", () =>
    {
        var cut = Render<NotFound>();

        cut.FindAll("h1").Should().ContainSingle();
        cut.FindAll("h1").Single().TextContent.Should().Contain("Not Found");
    });
}
