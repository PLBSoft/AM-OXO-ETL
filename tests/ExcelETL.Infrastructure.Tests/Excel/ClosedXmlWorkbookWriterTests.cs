using ClosedXML.Excel;
using ExcelETL.Application.Generation;
using ExcelETL.Infrastructure.Excel;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Excel;

public class ClosedXmlWorkbookWriterTests
{
    private readonly ClosedXmlWorkbookWriter _sut = new();

    [Fact]
    public void Write_WithMultipleSheets_WritesSheetNamesInOrder()
    {
        var workbook = new GeneratedWorkbook(
        [
            new GeneratedSheet("Parents", ["Repère"], []),
            new GeneratedSheet("Enfants", ["Repère"], [])
        ]);

        using var reread = WriteAndReread(workbook);

        reread.Worksheets.Select(ws => ws.Name).Should().Equal("Parents", "Enfants");
    }

    [Fact]
    public void Write_WithHeaders_WritesThemOnRowOne()
    {
        var workbook = new GeneratedWorkbook(
        [new GeneratedSheet("Parents", ["Repère", "Désignation"], [])]);

        using var reread = WriteAndReread(workbook);

        var worksheet = reread.Worksheet("Parents");
        worksheet.Cell(1, 1).GetString().Should().Be("Repère");
        worksheet.Cell(1, 2).GetString().Should().Be("Désignation");
    }

    [Fact]
    public void Write_WithDataRows_WritesCellValuesStartingRowTwo()
    {
        var workbook = new GeneratedWorkbook(
        [
            new GeneratedSheet(
                "Parents",
                ["Repère", "Désignation"],
                [new GeneratedRow(["38-C7401", "Compresseur C7401"]), new GeneratedRow(["38-D8570", "Vanne D8570"])])
        ]);

        using var reread = WriteAndReread(workbook);

        var worksheet = reread.Worksheet("Parents");
        worksheet.Cell(2, 1).GetString().Should().Be("38-C7401");
        worksheet.Cell(2, 2).GetString().Should().Be("Compresseur C7401");
        worksheet.Cell(3, 1).GetString().Should().Be("38-D8570");
        worksheet.Cell(3, 2).GetString().Should().Be("Vanne D8570");
    }

    [Fact]
    public void Write_WithEmptyCell_WritesBlankCell()
    {
        var workbook = new GeneratedWorkbook(
        [new GeneratedSheet("Parents", ["Repère", "Colonne libre"], [new GeneratedRow(["38-C7401", ""])])]);

        using var reread = WriteAndReread(workbook);

        reread.Worksheet("Parents").Cell(2, 2).GetString().Should().BeEmpty();
    }

    [Fact]
    public void Write_WithNullWorkbook_ThrowsArgumentNullException()
    {
        using var destination = new MemoryStream();

        var act = () => _sut.Write(null!, destination);

        act.Should().Throw<ArgumentNullException>();
    }

    private XLWorkbook WriteAndReread(GeneratedWorkbook workbook)
    {
        var destination = new MemoryStream();
        _sut.Write(workbook, destination);
        return new XLWorkbook(destination);
    }
}
