using ClosedXML.Excel;
using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Enums;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

public class ClosedXmlExtractionServiceTests
{
    private readonly ClosedXmlExtractionService _service = new();

    [Fact]
    public void Extract_WithMergedCellRange_ReadsValueFromTopLeftCell()
    {
        using var workbookStream = BuildWorkbook(ws =>
        {
            ws.Range("B2:D4").Merge();
            ws.Cell("B2").Value = "Acme Corp";
        });

        var config = new ExtractionConfig("Purchase Order Extraction");
        var sheet = new SheetConfig("Summary", sheetIndex: 0);
        sheet.AddCellMapping(new CellMapping("B2", "SupplierName", CellDataType.Text));
        config.AddSheet(sheet);

        var result = _service.Extract(workbookStream, config);

        result.GetValue("Summary", "SupplierName").Value.Should().Be("Acme Corp");
    }

    [Fact]
    public void Extract_WithMergedRangeConfiguredAsFullRange_ReadsSameValueAsTopLeftCell()
    {
        using var workbookStream = BuildWorkbook(ws =>
        {
            ws.Range("B2:D4").Merge();
            ws.Cell("B2").Value = "Acme Corp";
        });

        var config = new ExtractionConfig("Purchase Order Extraction");
        var sheet = new SheetConfig("Summary", sheetIndex: 0);
        sheet.AddCellMapping(new CellMapping("B2:D4", "SupplierName", CellDataType.Text));
        config.AddSheet(sheet);

        var result = _service.Extract(workbookStream, config);

        result.GetValue("Summary", "SupplierName").Value.Should().Be("Acme Corp");
    }

    [Fact]
    public void Extract_WithNumericAndDateCells_ParsesAccordingToConfiguredDataType()
    {
        using var workbookStream = BuildWorkbook(ws =>
        {
            ws.Cell("A1").Value = 42.5;
            ws.Cell("A2").Value = new DateTime(2026, 7, 10);
        });

        var config = new ExtractionConfig("Numeric Extraction");
        var sheet = new SheetConfig("Summary", sheetIndex: 0);
        sheet.AddCellMapping(new CellMapping("A1", "Total", CellDataType.Decimal));
        sheet.AddCellMapping(new CellMapping("A2", "IssuedOn", CellDataType.Date));
        config.AddSheet(sheet);

        var result = _service.Extract(workbookStream, config);

        result.GetValue("Summary", "Total").Value.Should().Be(42.5m);
        result.GetValue("Summary", "IssuedOn").Value.Should().Be(new DateTime(2026, 7, 10));
    }

    [Fact]
    public void Extract_WithMultipleSheets_ReadsFromMatchingSheetByName()
    {
        using var workbookStream = BuildWorkbook(
            ("Summary", ws => ws.Cell("A1").Value = "Summary Value"),
            ("Details", ws => ws.Cell("A1").Value = "Details Value"));

        var config = new ExtractionConfig("Multi-Sheet Extraction");

        var summarySheet = new SheetConfig("Summary", sheetIndex: 0);
        summarySheet.AddCellMapping(new CellMapping("A1", "Value", CellDataType.Text));
        config.AddSheet(summarySheet);

        var detailsSheet = new SheetConfig("Details", sheetIndex: 1);
        detailsSheet.AddCellMapping(new CellMapping("A1", "Value", CellDataType.Text));
        config.AddSheet(detailsSheet);

        var result = _service.Extract(workbookStream, config);

        result.GetValue("Summary", "Value").Value.Should().Be("Summary Value");
        result.GetValue("Details", "Value").Value.Should().Be("Details Value");
    }

    [Fact]
    public void Extract_WhenConfiguredSheetIsMissingFromWorkbook_ThrowsInvalidOperationException()
    {
        using var workbookStream = BuildWorkbook(ws => ws.Cell("A1").Value = "irrelevant");

        var config = new ExtractionConfig("Extraction With Missing Sheet");
        var sheet = new SheetConfig("DoesNotExist", sheetIndex: 0);
        sheet.AddCellMapping(new CellMapping("A1", "Value", CellDataType.Text));
        config.AddSheet(sheet);

        var act = () => _service.Extract(workbookStream, config);

        act.Should().Throw<InvalidOperationException>().WithMessage("*DoesNotExist*");
    }

    private static MemoryStream BuildWorkbook(Action<IXLWorksheet> configureSheet)
    {
        return BuildWorkbook(("Summary", configureSheet));
    }

    private static MemoryStream BuildWorkbook(params (string Name, Action<IXLWorksheet> Configure)[] sheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var (name, configure) in sheets)
        {
            var worksheet = workbook.Worksheets.Add(name);
            configure(worksheet);
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
