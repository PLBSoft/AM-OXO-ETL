using System.Text.Json.Serialization;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.BlazorAdmin.Editing;

namespace ExcelETL.BlazorAdmin.Editing.Export;

// Mutable, behavior-free mirror of PointColumnDefinition (Domain) -- see PointColumnDefinitionForm.razor
// (Blazor-only prior default of "X", identical to Domain's own PointColumnDefinition.DefaultMarkValue).
public sealed class PointColumnDefinitionDraft : IDraftWithError
{
    public string ColonneNom { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string MarkValue { get; set; } = PointColumnDefinition.DefaultMarkValue;

    [JsonIgnore]
    public string? Error { get; set; }
}
