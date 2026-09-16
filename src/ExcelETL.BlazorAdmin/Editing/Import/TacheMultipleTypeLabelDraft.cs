using System.Text.Json.Serialization;

namespace ExcelETL.BlazorAdmin.Editing.Import;

// Mutable, behavior-free mirror of TacheMultipleTypeLabel (Domain).
public sealed class TacheMultipleTypeLabelDraft : IDraftWithError
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    [JsonIgnore]
    public string? Error { get; set; }
}
