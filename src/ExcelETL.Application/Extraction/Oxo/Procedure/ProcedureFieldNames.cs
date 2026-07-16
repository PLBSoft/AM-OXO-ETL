namespace ExcelETL.Application.Extraction.Oxo.Procedure;

// Field names used both when building a PROCEDURE SheetExtractionRule's RepeatingBlockLocator and by
// ProcedureExtractionService's own block loop -- kept as constants so the two stay in sync (see
// ProcedureExtractionService for why it can't delegate to the shared IRepeatingBlockReader).
public static class ProcedureFieldNames
{
    public const string Action = "Action";
    public const string Ordre = "Ordre";
    public const string Acteur = "Acteur";
    public const string Risques = "Risques";
    public const string TypeTacheMultipleAlias = "TypeTacheMultipleAlias";
    public const string DateValidation = "DateValidation";
}
