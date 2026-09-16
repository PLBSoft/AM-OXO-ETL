namespace ExcelETL.BlazorAdmin.Editing;

// Implemented by every draft type (Editing/Export/*Draft.cs) so a caller that only has the offending
// draft as `object` (e.g. from a DraftConversionError) can set its display-only Error property
// generically, without a per-type switch.
public interface IDraftWithError
{
    string? Error { get; set; }
}
