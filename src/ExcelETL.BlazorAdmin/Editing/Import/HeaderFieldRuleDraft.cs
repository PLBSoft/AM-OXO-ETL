using System.Text.Json.Serialization;

namespace ExcelETL.BlazorAdmin.Editing.Import;

// Mutable, behavior-free mirror of HeaderFieldRule (Domain). No sheet name: the cell always reads the
// owning rule's own sheet (lot 048, decision 2), applied by ImportProfileDraftMapper. DateFormat blank
// means "no date reformatting" (null in the Domain).
public sealed class HeaderFieldRuleDraft : IDraftWithError
{
    public string Name { get; set; } = string.Empty;
    public string Range { get; set; } = string.Empty;
    public string DateFormat { get; set; } = string.Empty;
    public bool StripReperePrefix { get; set; }

    [JsonIgnore]
    public string? Error { get; set; }
}
