using System.Text.Json.Serialization;

namespace ExcelETL.BlazorAdmin.Editing.Import;

// Mutable, behavior-free draft for one entry of a plain string list -- shared by
// ImportProfile.DefaultTableaux, ImportProfile.DefaultApplicationNames and
// SheetExtractionRule.UnconditionalColonneNames (lot 075, decision Q2: one class rather than three
// identical ones). An object rather than a bare string so it can carry its own display-only Error and
// be the target of a DraftConversionError, like every other draft.
public sealed class StringItemDraft : IDraftWithError
{
    public string Value { get; set; } = string.Empty;

    [JsonIgnore]
    public string? Error { get; set; }
}
