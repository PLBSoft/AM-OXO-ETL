using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Sections;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Layout;

// X10 (Lot X): feasibility check for SectionContent/SectionOutlet under bUnit's TestContext --
// a blocking prerequisite for X11 (back-nav link merged into the shared top-row banner). See
// docs/tickets-tdd-blazor-polish-ux-lot-x.md. FakeHost is deliberately representative of
// MainLayout/NavMenu's real top-row + body structure, not an artificially minimal component, so a
// pass here is trustworthy evidence for X11's real layout.
//
// Result (2026-07-24): both tests below pass with ZERO extra service registration -- bUnit's
// default TestContext (as configured by BunitContext in this project) already resolves the
// SectionRegistry SectionOutlet/SectionContent need. No fallback service, no custom
// TestContext.Services.Add* call was required. X11 may proceed using SectionOutlet/SectionContent
// directly in MainLayout/NavMenu with no additional test-infrastructure changes.
public class SectionOutletFeasibilityTests : BunitContext
{
    private const string SectionName = "test-section";

    private sealed class FakeHost : ComponentBase
    {
        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "top-row");
            builder.OpenComponent<SectionOutlet>(2);
            builder.AddComponentParameter(3, nameof(SectionOutlet.SectionName), SectionName);
            builder.CloseComponent();
            builder.CloseElement();

            builder.OpenElement(4, "main");
            builder.AddContent(5, ChildContent);
            builder.CloseElement();
        }
    }

    private sealed class FakeChildWithSectionContent : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<SectionContent>(0);
            builder.AddComponentParameter(1, nameof(SectionContent.SectionName), SectionName);
            builder.AddComponentParameter(
                2,
                nameof(SectionContent.ChildContent),
                (RenderFragment)(b => b.AddContent(0, "Contenu de test")));
            builder.CloseComponent();
        }
    }

    [Fact]
    public void SectionOutlet_RendersSectionContent_FromNestedChildComponent()
    {
        var cut = Render<FakeHost>(parameters => parameters
            .Add(p => p.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<FakeChildWithSectionContent>(0);
                b.CloseComponent();
            })));

        var topRow = cut.Find(".top-row");
        topRow.TextContent.Should().Contain("Contenu de test");
    }

    [Fact]
    public void SectionOutlet_SeparateTestInstance_DoesNotLeakContentFromAnotherTest()
    {
        // No SectionContent is rendered anywhere in this test -- if SectionRegistry state leaked
        // across tests (e.g. a singleton registered too broadly by bUnit's default services), the
        // previous test's "Contenu de test" would unexpectedly reappear here too.
        var cut = Render<FakeHost>();

        var topRow = cut.Find(".top-row");
        topRow.TextContent.Should().NotContain("Contenu de test");
    }
}
