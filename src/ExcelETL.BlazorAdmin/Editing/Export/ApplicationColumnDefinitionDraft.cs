using System.Text.Json.Serialization;
using ExcelETL.BlazorAdmin.Editing;

namespace ExcelETL.BlazorAdmin.Editing.Export;

// Mutable, behavior-free mirror of ApplicationColumnDefinition (Domain). Default MarkValue of "O" is
// the pre-existing Blazor-only pre-fill (distinct from ApplicationColumnDefinition.DefaultMarkValue's
// "X") -- matches the one real seeded Application today (PROGRESS -> "O"), a UX choice, not a Domain
// default (see the pre-migration ApplicationColumnDefinitionForm.razor's own comment).
public sealed class ApplicationColumnDefinitionDraft : IDraftWithError
{
    public string ApplicationNom { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string MarkValue { get; set; } = "O";

    [JsonIgnore]
    public string? Error { get; set; }
}
