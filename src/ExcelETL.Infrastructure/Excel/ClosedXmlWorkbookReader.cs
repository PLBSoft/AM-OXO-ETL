using System.Globalization;
using ClosedXML.Excel;
using ExcelETL.Application.Extraction;
using ExcelETL.Application.Extraction.Oxo;

namespace ExcelETL.Infrastructure.Excel;

// The Lot E1 implementation of the OXO pipeline's IWorkbookReader -- opens the stream once and keeps
// it open for the lifetime of the reader, so it owns disposal of the underlying XLWorkbook (unlike
// IWorkbookReader itself, whose Lot B/C consumers never need to know about disposal). Reuses
// WorksheetNotFoundInWorkbookException from the pre-existing ExtractionConfig pipeline rather than
// declaring a near-duplicate exception type for the same "sheet not found" condition.
public sealed class ClosedXmlWorkbookReader : IWorkbookReader, IDisposable
{
    private readonly XLWorkbook _workbook;

    public ClosedXmlWorkbookReader(Stream excelFileStream)
    {
        ArgumentNullException.ThrowIfNull(excelFileStream);
        _workbook = new XLWorkbook(excelFileStream);
    }

    public bool SheetExists(string sheet) => _workbook.Worksheets.TryGetWorksheet(sheet, out _);

    public string? ReadCellValue(string sheet, string range)
    {
        if (!_workbook.Worksheets.TryGetWorksheet(sheet, out var worksheet))
        {
            throw new WorksheetNotFoundInWorkbookException(sheet);
        }

        var cell = worksheet.Cell(TopLeftCellAddress(range));

        // GetString() renders a date-typed cell using CultureInfo.CurrentCulture, which drifts under
        // ASP.NET Core's per-request culture negotiation (RequestLocalizationOptions) -- discovered at
        // Lot K1 when this pipeline ran, for the first time, inside a host that negotiates request
        // culture (WebAPI defaults to en-US): the same cell that renders "12/12/2025 00:00:00" under a
        // plain xUnit process (ambient culture) instead renders "9/11/2025 12:00:00 AM", which
        // ProcedureExtractionService.TryParseDate's fixed "dd/MM/yyyy HH:mm:ss"/"dd/MM/yyyy" formats
        // can't parse -- silently rejecting every real fixture. Render date cells ourselves,
        // culture-invariant, rather than trust GetString()'s culture-dependent formatting.
        if (cell.DataType == XLDataType.DateTime)
        {
            return cell.GetDateTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        }

        var value = cell.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void Dispose() => _workbook.Dispose();

    private static string TopLeftCellAddress(string range)
    {
        var colonIndex = range.IndexOf(':');
        return colonIndex >= 0 ? range[..colonIndex] : range;
    }
}
