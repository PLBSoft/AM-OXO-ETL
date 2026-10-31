namespace ExcelETL.Domain.Archiving;

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, D10/D13): one page of a plan sheet,
// printed to its own PDF and archived next to the generated workbook. FilePath is relative to the
// archive root, like GeneratedFileRecord.SourceFilePath. Built only internally by ProcessOxoFileService.
public sealed record GeneratedFilePlan(string SheetName, int PageNumber, string FileName, string FilePath, long SizeBytes);
