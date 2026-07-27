namespace ExcelETL.Application.Extraction.Oxo.Procedure;

// The HeaderFieldRule/HeaderCompositeRule Names ProcedureExtractionService looks up on PROCEDURE's
// SheetExtractionRule (Lot 047) -- the profile-driven replacement for the M2:O2/P2:Q2/R2:T2
// coordinates and the "Rév {revision} du {dateRev}" template previously hardcoded here.
public static class ProcedureHeaderFieldNames
{
    public const string NomMad = "nomMAD";
    public const string Revision = "revision";
    public const string DateRev = "dateRev";
    public const string Designation = "Designation";
}
