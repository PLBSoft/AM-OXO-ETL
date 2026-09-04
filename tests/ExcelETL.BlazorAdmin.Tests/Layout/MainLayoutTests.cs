using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Bunit;
using ExcelETL.BlazorAdmin.Components.Layout;
using ExcelETL.BlazorAdmin.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Layout;

public class MainLayoutTests : BunitContext
{
    public MainLayoutTests()
    {
        Services.AddLocalization();
        this.AddAuthorization().SetNotAuthorized();

        // Lot 062 (62.3): MainLayout renders NavMenu, which now injects ApplicationBuildInfo. Must be
        // registered before SetRendererInfo below -- that call resolves a service internally, which
        // locks the bUnit service provider against any further registration.
        Services.AddSingleton(new ApplicationBuildInfo(System.Reflection.Assembly.GetExecutingAssembly()));
        // Follow-up (post-064): NavMenu also injects ILocalTimeFormatter for its build-date footer --
        // no Setup needed, the default ctor-registered ApplicationBuildInfo above has no BuildDate,
        // so NavMenu's OnAfterRenderAsync never actually calls into it.
        Services.AddSingleton(Mock.Of<ILocalTimeFormatter>());

        // Lot 045 (45.4): MainLayout now renders PasswordChangeGuard, which reads RendererInfo to
        // decide whether to render <NavigationLock> (only supported in an interactive render mode --
        // matches this app's global InteractiveServer rendermode for every non-Account page).
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
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

        cut.Markup.Should().Contain("An unhandled error has occurred.");
        cut.Markup.Should().Contain("Reload");
    });

    [Fact]
    public void MainLayout_WithFrenchCulture_RendersFrenchChrome() => WithCulture("fr-FR", () =>
    {
        var cut = Render<MainLayout>();

        cut.Markup.Should().Contain("Une erreur non gérée s'est produite.");
        cut.Markup.Should().Contain("Recharger");
    });
}
