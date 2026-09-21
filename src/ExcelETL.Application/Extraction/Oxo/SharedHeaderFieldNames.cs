namespace ExcelETL.Application.Extraction.Oxo;

// The HeaderFieldRule.Name every element sheet's equipment repère echo is looked up by (Lot 047 for
// AUTRES JOINTS TOUCHES/DIVERS, all five element sheets since lot 084) -- same naming convention as
// Procedure.ProcedureHeaderFieldNames for PROCEDURE's own header fields.
public static class SharedHeaderFieldNames
{
    public const string RepereEcho = "repereEcho";
}
