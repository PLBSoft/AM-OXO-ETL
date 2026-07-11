using ClosedXML.Excel;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction;
using ExcelETL.Domain.Enums;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

public class ClosedXmlGeneratorServiceTests
{
    private readonly ClosedXmlGeneratorService _service = new(NullLogger<ClosedXmlGeneratorService>.Instance);

    [Fact]
    public void Generate_WithFourSheets_ProducesWorkbookWithFourSheets()
    {
        var result = BuildExtractionResult(sheetCount: 4);

        using var stream = _service.Generate(result);
        using var workbook = new XLWorkbook(stream);

        workbook.Worksheets.Count.Should().Be(4);
    }

    [Fact]
    public void Generate_WithFiveSheets_ProducesWorkbookWithFiveSheets()
    {
        var result = BuildExtractionResult(sheetCount: 5);

        using var stream = _service.Generate(result);
        using var workbook = new XLWorkbook(stream);

        workbook.Worksheets.Count.Should().Be(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void Generate_WithSheetCountOutsideFourToFiveRange_ThrowsInvalidGeneratedWorkbookSheetCountException(
        int sheetCount)
    {
        var result = BuildExtractionResult(sheetCount);

        var act = () => _service.Generate(result);

        act.Should().Throw<InvalidGeneratedWorkbookSheetCountException>()
            .Which.Should().Match<InvalidGeneratedWorkbookSheetCountException>(ex =>
                ex.SheetCount == sheetCount
                && ex.MinSheets == 4
                && ex.MaxSheets == 5
                && ex.ErrorCode == ApplicationErrorCode.GeneratedWorkbookSheetCountOutOfRange);
    }

    [Fact]
    public void Generate_WritesTargetPropertyNameAndValueForEachMapping()
    {
        var summarySheet = new ExtractedSheet("Summary",
        [
            new ExtractedValue("SupplierName", "Acme Corp", CellDataType.Text),
            new ExtractedValue("Total", 42.5m, CellDataType.Decimal)
        ]);
        var sheets = PadToMinimumSheetCount([summarySheet]);
        var result = new ExtractionResult(sheets);

        using var stream = _service.Generate(result);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet("Summary");

        worksheet.Cell(2, 1).GetString().Should().Be("SupplierName");
        worksheet.Cell(2, 2).GetString().Should().Be("Acme Corp");
        worksheet.Cell(3, 1).GetString().Should().Be("Total");
        worksheet.Cell(3, 2).GetValue<decimal>().Should().Be(42.5m);
    }

    private static ExtractionResult BuildExtractionResult(int sheetCount)
    {
        var sheets = Enumerable.Range(1, sheetCount)
            .Select(i => new ExtractedSheet($"Sheet{i}",
            [
                new ExtractedValue("Property", $"Value{i}", CellDataType.Text)
            ]))
            .ToList();

        return new ExtractionResult(sheets);
    }

    private static List<ExtractedSheet> PadToMinimumSheetCount(List<ExtractedSheet> sheets)
    {
        var padded = new List<ExtractedSheet>(sheets);
        var index = padded.Count;
        while (padded.Count < 4)
        {
            padded.Add(new ExtractedSheet($"Padding{index++}",
            [
                new ExtractedValue("Property", "Value", CellDataType.Text)
            ]));
        }

        return padded;
    }
}
