namespace ExcelETL.Application.Generation;

// The full intermediate output of one generation run -- one GeneratedSheet per SheetGenerationRule,
// in the ExportProfile's order. Infrastructure (I4) is the only layer that turns this into real
// ClosedXML/.xlsx bytes.
public sealed record GeneratedWorkbook(IReadOnlyList<GeneratedSheet> Sheets);
