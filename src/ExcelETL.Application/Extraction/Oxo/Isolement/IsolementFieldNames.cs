namespace ExcelETL.Application.Extraction.Oxo.Isolement;

// Field names used both when building an ISOLEMENT SheetExtractionRule's RepeatingBlockLocator and
// by IsolementExtractionService's own block loop -- see IsolementExtractionService for why it can't
// delegate to the shared IRepeatingBlockReader. TypeElement matches the SourceFieldName convention
// used by ConditionalPointRule (see model doc §1.4's own "TypeElement" example).
public static class IsolementFieldNames
{
    public const string Identification = "Identification";
    public const string Designation = "Designation";
    public const string PositionALaPose = "PositionALaPose";
    public const string TypeElement = "TypeElement";

    // Lot 063: the dedicated "zero energie" cell (column V), optional in the RepeatingBlockLocator --
    // a profile that doesn't configure it (predating this lot) keeps working unchanged. Used both as
    // this field's BlockFieldDefinition.Name and, once read, as the extracted-field key ("true"/
    // "false") the PS941 ConditionalPointRule's SourceFieldName targets -- see
    // IsolementExtractionService.
    public const string HasZeroEnergie = "HasZeroEnergie";
}
