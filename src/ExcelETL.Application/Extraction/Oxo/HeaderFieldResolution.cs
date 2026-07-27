namespace ExcelETL.Application.Extraction.Oxo;

// One HeaderFieldRule's resolution outcome. RawValue is what IWorkbookReader returned before any
// transform (needed by callers like ProcedureExtractionService that must distinguish "blank cell" from
// "prefix mismatch" for different ExtractionErrorCodes -- the resolver itself stays policy-free about
// what counts as required). Value is the raw value after StripReperePrefix/DateFormat have been
// applied, or null if either transform failed (see ErrorMessage).
public sealed record HeaderFieldResolution(string Name, string? RawValue, string? Value, string? ErrorMessage);
