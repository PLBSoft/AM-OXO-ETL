using System.Globalization;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
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
        try
        {
            _workbook = new XLWorkbook(WithoutCellComments(excelFileStream));
        }
        catch (Exception exception) when (exception is not FileFormatException)
        {
            // A real .xlsx package ClosedXML can't load: reported to the user as a rejected file
            // (ProcessOxoFileService). Not an Excel package at all stays FileFormatException (400).
            throw new UnreadableWorkbookException(exception);
        }
    }

    // ClosedXML 0.105.1 throws "Sequence contains no matching element" while loading a note whose
    // VML shape has no x:ClientData or v:textbox element (files saved by some non-Excel tools) --
    // seen in production on POST /api/oxo/process. The extraction never reads comments, so their
    // part is removed from a copy of the file before ClosedXML opens it. The caller's stream (and
    // the archived source file) is left untouched.
    private static MemoryStream WithoutCellComments(Stream excelFileStream)
    {
        var copy = new MemoryStream();
        excelFileStream.CopyTo(copy);
        copy.Position = 0;

        if (HasCellComments(copy))
        {
            using var document = SpreadsheetDocument.Open(copy, isEditable: true);
            foreach (var worksheetPart in document.WorkbookPart!.WorksheetParts)
            {
                if (worksheetPart.WorksheetCommentsPart is { } commentsPart)
                {
                    worksheetPart.DeletePart(commentsPart);
                }
            }
        }

        copy.Position = 0;
        return copy;
    }

    // Opened read-only first so a file without comments (the usual case) is never rewritten. A
    // stream that isn't a valid package is left for XLWorkbook to reject with its own exception
    // (FileFormatException, translated to a 400 by OxoController).
    private static bool HasCellComments(MemoryStream copy)
    {
        try
        {
            using var document = SpreadsheetDocument.Open(copy, isEditable: false);
            return document.WorkbookPart?.WorksheetParts.Any(p => p.WorksheetCommentsPart is not null) ?? false;
        }
        catch (Exception exception) when (exception is FileFormatException or OpenXmlPackageException or InvalidDataException)
        {
            return false;
        }
        finally
        {
            copy.Position = 0;
        }
    }

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
