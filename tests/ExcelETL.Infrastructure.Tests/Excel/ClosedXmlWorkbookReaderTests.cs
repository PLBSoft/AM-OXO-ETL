using ClosedXML.Excel;
using ExcelETL.Application.Extraction;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

public class ClosedXmlWorkbookReaderTests
{
    [Fact]
    public void ReadCellValue_WithMergedRange_ReturnsTopLeftCellValue()
    {
        using var stream = BuildWorkbook(ws =>
        {
            ws.Range("B2:D4").Merge();
            ws.Cell("B2").Value = "Acme Corp";
        });
        using var sut = new ClosedXmlWorkbookReader(stream);

        sut.ReadCellValue("Sheet", "B2:D4").Should().Be("Acme Corp");
    }

    [Fact]
    public void ReadCellValue_WithSingleCell_ReturnsItsValue()
    {
        using var stream = BuildWorkbook(ws => ws.Cell("M2").Value = "MAD-OXO-38-C7401");
        using var sut = new ClosedXmlWorkbookReader(stream);

        sut.ReadCellValue("Sheet", "M2").Should().Be("MAD-OXO-38-C7401");
    }

    [Fact]
    public void ReadCellValue_WithBlankCell_ReturnsNull()
    {
        using var stream = BuildWorkbook(_ => { });
        using var sut = new ClosedXmlWorkbookReader(stream);

        sut.ReadCellValue("Sheet", "A1").Should().BeNull();
    }

    [Fact]
    public void ReadCellValue_WithUnknownSheet_ThrowsWorksheetNotFoundInWorkbookException()
    {
        using var stream = BuildWorkbook(_ => { });
        using var sut = new ClosedXmlWorkbookReader(stream);

        var act = () => sut.ReadCellValue("MISSING", "A1");

        act.Should().Throw<WorksheetNotFoundInWorkbookException>()
            .Which.SheetName.Should().Be("MISSING");
    }

    [Fact]
    public void SheetExists_WithExistingSheet_ReturnsTrue()
    {
        using var stream = BuildWorkbook(_ => { });
        using var sut = new ClosedXmlWorkbookReader(stream);

        sut.SheetExists("Sheet").Should().BeTrue();
    }

    [Fact]
    public void SheetExists_WithMissingSheet_ReturnsFalse()
    {
        using var stream = BuildWorkbook(_ => { });
        using var sut = new ClosedXmlWorkbookReader(stream);

        sut.SheetExists("MISSING").Should().BeFalse();
    }

    private static MemoryStream BuildWorkbook(Action<IXLWorksheet> configureSheet)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet");
        configureSheet(worksheet);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
