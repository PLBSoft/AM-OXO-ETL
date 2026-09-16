using System.Text.Json.Serialization;
using ExcelETL.BlazorAdmin.Editing;

namespace ExcelETL.BlazorAdmin.Editing.Export;

// Mutable, behavior-free mirror of ColumnDefinition (Domain) -- see ExportProfileDraftMapper for the
// two pure conversions to/from the real Domain type. SourceValue holds the raw <select> value: ""
// means "not mapped" (null Source), anything else is a PivotFieldRef.ToString().
public sealed class ColumnDefinitionDraft : IDraftWithError
{
    public string Header { get; set; } = string.Empty;
    public string SourceValue { get; set; } = string.Empty;

    // Display-only: set by whichever component owns this draft's list once a conversion attempt
    // fails, read by the leaf form that renders this draft's own fields. Never serialized (DraftJson
    // must never let this affect Clone/IsPristine).
    [JsonIgnore]
    public string? Error { get; set; }
}
