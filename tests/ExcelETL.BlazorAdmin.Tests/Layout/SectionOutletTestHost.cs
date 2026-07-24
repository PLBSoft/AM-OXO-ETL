using ExcelETL.BlazorAdmin.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Sections;

namespace ExcelETL.BlazorAdmin.Tests.Layout;

// X11 (Lot X): shared test-infra host, representative of NavMenu's real top-row structure
// (SectionOutlet + navbar-brand link), used by page tests that need to assert a <PageBackNavLink>
// is actually projected into the shared banner rather than left in the page's own content flow.
// Colocated here (not fixture data, generic infra) per the same precedent as
// legacy/ExcelProcessingClientService.Tests/FakeHttpMessageHandler.cs -- one shared infra class per
// test project is acceptable, this repo's "no shared test helper" convention targets fixture/data
// helpers, not this kind of structural test double.
public sealed class SectionOutletTestHost : ComponentBase
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "top-row");

        builder.OpenComponent<SectionOutlet>(2);
        builder.AddComponentParameter(3, nameof(SectionOutlet.SectionName), NavMenu.PageBackNavSectionName);
        builder.CloseComponent();

        builder.OpenElement(4, "a");
        builder.AddAttribute(5, "class", "navbar-brand");
        builder.AddContent(6, "Alpha - MAD / REL OXO");
        builder.CloseElement();

        builder.CloseElement();

        builder.OpenElement(7, "main");
        builder.AddContent(8, ChildContent);
        builder.CloseElement();
    }
}
