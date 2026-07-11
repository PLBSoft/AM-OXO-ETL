using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Bunit;
using ExcelETL.BlazorAdmin.Components.Layout;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Layout;

public class ReconnectModalTests : BunitContext
{
    public ReconnectModalTests() => Services.AddLocalization();

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
    public void ReconnectModal_WithEnglishCulture_RendersEnglishText() => WithCulture("en-US", () =>
    {
        var cut = Render<ReconnectModal>();

        cut.Markup.Should().Contain("Rejoining the server...");
        cut.Markup.Should().Contain("Retry");
        cut.Markup.Should().Contain("Resume");
    });

    [Fact]
    public void ReconnectModal_WithFrenchCulture_RendersFrenchText() => WithCulture("fr-FR", () =>
    {
        var cut = Render<ReconnectModal>();

        cut.Markup.Should().Contain("Reconnexion au serveur...");
        cut.Markup.Should().Contain("Réessayer");
        cut.Markup.Should().Contain("Reprendre");
    });
}
