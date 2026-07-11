using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction;
using ExcelETL.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Application.Tests.Extraction;

public class ExtractionResultTests
{
    private static ExtractionResult CreateResult() => new([
        new ExtractedSheet("Summary", [new ExtractedValue("InvoiceNumber", "INV-1", CellDataType.Text)])
    ]);

    [Fact]
    public void GetValue_WithExistingSheetAndProperty_ReturnsValue()
    {
        var result = CreateResult();

        var value = result.GetValue("Summary", "InvoiceNumber");

        value.Value.Should().Be("INV-1");
    }

    [Fact]
    public void GetValue_WithUnknownSheet_ThrowsExtractionResultLookupException()
    {
        var result = CreateResult();

        var act = () => result.GetValue("Details", "InvoiceNumber");

        act.Should().Throw<ExtractionResultLookupException>()
            .WithMessage("*Details*")
            .Which.ErrorCode.Should().Be(ApplicationErrorCode.ExtractionResult_SheetNotFound);
    }

    [Fact]
    public void GetValue_WithUnknownProperty_ThrowsExtractionResultLookupException()
    {
        var result = CreateResult();

        var act = () => result.GetValue("Summary", "Total");

        act.Should().Throw<ExtractionResultLookupException>()
            .WithMessage("*Total*")
            .Which.ErrorCode.Should().Be(ApplicationErrorCode.ExtractionResult_PropertyNotFound);
    }
}
