using System.Linq;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace ExcelETL.BlazorAdmin.Tests.Pages;

// Lot 042 (42.2): generic guard-rail against heading-level skips (h1 -> h3 without an h2, etc.),
// reused across every page the audit flagged rather than one hand-written assertion per page --
// mirrors the FormFloatingStructureAssertions precedent (Lot 030).
internal static class HeadingHierarchyAssertions
{
    public static void AssertNoHeadingLevelSkip<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        var headings = cut.FindAll("h1,h2,h3,h4,h5,h6");
        headings.Should().NotBeEmpty("this page is expected to render at least one heading for this audit to be meaningful");

        var levels = headings.Select(h => int.Parse(h.TagName[1..])).ToList();

        levels.Count(l => l == 1).Should().Be(1, "a page must have exactly one h1");
        levels[0].Should().Be(1, "the first heading rendered on the page must be its h1 title");

        for (var i = 1; i < levels.Count; i++)
        {
            if (levels[i] > levels[i - 1])
            {
                levels[i].Should().Be(
                    levels[i - 1] + 1,
                    $"heading level jumped from h{levels[i - 1]} to h{levels[i]} without an intermediate h{levels[i - 1] + 1}");
            }
        }
    }
}
