namespace ExcelETL.Application.Extraction.Oxo.Platines;

// Field names used both when building a PLATINES SheetExtractionRule's RepeatingBlockLocator and by
// PlatinesExtractionService. Unlike PROCEDURE/ISOLEMENT, every field here is genuinely required
// (confirmed against all 3 real fixtures -- no blanks observed), so PlatinesExtractionService can
// delegate to the shared IRepeatingBlockReader instead of walking the block itself.
public static class PlatinesFieldNames
{
    public const string Identification = "Identification";
    public const string Designation = "Designation";
    public const string TypeElement = "TypeElement";
}
