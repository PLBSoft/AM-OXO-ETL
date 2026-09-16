using ExcelETL.BlazorAdmin.Editing;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Editing;

// Lot 075.1: a presentation-layer validation failure (e.g. an unparsable Excel range) that has no
// Domain exception to carry it -- it names a BlazorAdminMessages resource key, localized by the page.
public class DraftValidationExceptionTests
{
    [Fact]
    public void CarriesItsResourceKey()
    {
        var exception = new DraftValidationException("ImportProfileEditor_InvalidExcelRangeError");

        exception.ResourceKey.Should().Be("ImportProfileEditor_InvalidExcelRangeError");
        exception.Message.Should().Contain("ImportProfileEditor_InvalidExcelRangeError");
    }
}
