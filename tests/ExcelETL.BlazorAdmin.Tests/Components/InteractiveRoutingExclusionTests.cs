using Bunit;
using ExcelETL.BlazorAdmin.Components.Pages;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Components;

// Lot 049 (49.0): characterizes the framework behaviour that makes App.razor's per-request render
// mode mandatory, rather than leaving it as a claim in a document. The Router's *interactive* route
// table genuinely omits every page carrying [ExcludeFromInteractiveRouting] (applied to all of
// Components/Account/ via its _Imports.razor), so any such page handed an interactive render mode
// gets its correctly-rendered body replaced by NotFoundPage as soon as the circuit boots.
//
// This renders the framework's own Router with the same AppAssembly and NotFoundPage as
// Components/Routes.razor, so it fails if that exclusion behaviour ever changes -- in either
// direction -- and App.razor's compensation silently becomes wrong.
public class InteractiveRoutingExclusionTests : BunitContext
{
    public InteractiveRoutingExclusionTests() => Services.AddLocalization();

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ForcePasswordChange")]
    public void InteractiveRouteTable_ExcludesAccountPages(string url)
    {
        ResolveInteractively(url).Should().Be(typeof(NotFound));
    }

    [Theory]
    [InlineData("/import-profiles")]
    [InlineData("/export-profiles")]
    [InlineData("/users")]
    public void InteractiveRouteTable_IncludesAdminPages(string url)
    {
        ResolveInteractively(url).Should().NotBe(typeof(NotFound));
    }

    private Type ResolveInteractively(string url)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);

        Type? resolved = null;
        Render<Router>(parameters => parameters
            .Add(router => router.AppAssembly, typeof(Program).Assembly)
            .Add(router => router.Found, routeData =>
            {
                resolved = routeData.PageType;
                return builder => { };
            })
            .Add(router => router.NotFoundPage, typeof(NotFound)));

        return resolved!;
    }
}
