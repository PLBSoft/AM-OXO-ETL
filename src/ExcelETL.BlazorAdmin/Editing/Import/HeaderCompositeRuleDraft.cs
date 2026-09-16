using System.Text.Json.Serialization;

namespace ExcelETL.BlazorAdmin.Editing.Import;

// Mutable, behavior-free mirror of HeaderCompositeRule (Domain).
public sealed class HeaderCompositeRuleDraft : IDraftWithError
{
    public string Name { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;

    [JsonIgnore]
    public string? Error { get; set; }
}
