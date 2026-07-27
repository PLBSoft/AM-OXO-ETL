namespace ExcelETL.Application.Extraction.Oxo;

// The HeaderFieldRule.Name every isolement-style sheet's repere echo (Lot 047, previously the "N6"
// coordinate hardcoded in AutresJointsTouchesExtractionService/DiversExtractionService) is looked up
// by. Shared by both services since it's the exact same logical field -- same naming convention as
// Procedure.ProcedureHeaderFieldNames for PROCEDURE's own header fields.
public static class SharedHeaderFieldNames
{
    public const string RepereEcho = "repereEcho";
}
