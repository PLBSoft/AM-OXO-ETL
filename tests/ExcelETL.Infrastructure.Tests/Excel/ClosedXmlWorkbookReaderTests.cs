using System.Xml.Linq;
using ClosedXML.Excel;
using ExcelETL.Application.Extraction.Oxo;
using DocumentFormat.OpenXml.Packaging;
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
    public void Constructor_WithCommentShapeClosedXmlCannotLoad_StillReadsCellValues()
    {
        // A note whose VML shape has no x:ClientData element (files saved by some non-Excel tools):
        // ClosedXML 0.105.1 throws "Sequence contains no matching element" while loading it. The
        // extraction never reads comments, so the reader must open the file anyway.
        using var stream = BuildWorkbookWithCommentShapeWithoutClientData();
        var loadWithClosedXmlAlone = () => new XLWorkbook(new MemoryStream(stream.ToArray()));
        loadWithClosedXmlAlone.Should().Throw<InvalidOperationException>();

        using var sut = new ClosedXmlWorkbookReader(stream);

        sut.ReadCellValue("Sheet", "B2").Should().Be("Acme Corp");
    }

    [Fact]
    public void Constructor_WhenClosedXmlFailsToLoadAValidPackage_ThrowsUnreadableWorkbookException()
    {
        using var stream = BuildWorkbookWithSheetPartMissing();

        var act = () => new ClosedXmlWorkbookReader(stream);

        act.Should().Throw<UnreadableWorkbookException>().Which.InnerException.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithBytesThatAreNotAnExcelPackage_StillThrowsFileFormatException()
    {
        using var stream = new MemoryStream("not an excel file"u8.ToArray());

        var act = () => new ClosedXmlWorkbookReader(stream);

        act.Should().Throw<FileFormatException>();
    }

    [Fact]
    public void Factory_OpensTheBytesAsAReader()
    {
        using var stream = BuildWorkbook(ws => ws.Cell("B2").Value = "Acme Corp");

        var reader = new ClosedXmlWorkbookReaderFactory().Open(stream.ToArray());

        reader.ReadCellValue("Sheet", "B2").Should().Be("Acme Corp");
        ((IDisposable)reader).Dispose();
    }

    // A second sheet is declared in workbook.xml but its part is gone: a valid package ClosedXML
    // can't load.
    private static MemoryStream BuildWorkbookWithSheetPartMissing()
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            workbook.Worksheets.Add("Sheet");
            workbook.Worksheets.Add("Other");
            workbook.SaveAs(stream);
        }

        using (var document = SpreadsheetDocument.Open(stream, isEditable: true))
        {
            var workbookPart = document.WorkbookPart!;
            workbookPart.DeletePart(workbookPart.WorksheetParts.Last());
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildWorkbookWithCommentShapeWithoutClientData()
    {
        var stream = BuildWorkbook(ws =>
        {
            ws.Cell("B2").Value = "Acme Corp";
            ws.Cell("B2").CreateComment().AddText("Note");
        });

        using (var document = SpreadsheetDocument.Open(stream, isEditable: true))
        {
            foreach (var vmlPart in document.WorkbookPart!.WorksheetParts.SelectMany(p => p.VmlDrawingParts))
            {
                XDocument vml;
                using (var read = vmlPart.GetStream(FileMode.Open))
                {
                    vml = XDocument.Load(read);
                }

                vml.Descendants().Where(e => e.Name.LocalName == "ClientData").ToList().ForEach(e => e.Remove());
                using var write = vmlPart.GetStream(FileMode.Create);
                vml.Save(write);
            }
        }

        stream.Position = 0;
        return stream;
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
