using System.Text.Json.Serialization;

namespace ExcelETL.BlazorAdmin.Editing.Import;

// Mutable, behavior-free mirror of FieldPresencePointRule (Domain). AbsoluteRange follows the same
// source-of-truth rule as BlockFieldDefinitionDraft.AbsoluteRange. CellName carries the stored name of
// the cell, which no input exposes: null for a new rule (the mapper then applies its default name), the
// original name otherwise -- so editing a seeded rule never renames its cell. ExpectedValue blank means
// a presence-only rule (null in the Domain).
public sealed class FieldPresencePointRuleDraft : IDraftWithError
{
    public string ColonneName { get; set; } = string.Empty;
    public string AbsoluteRange { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string? CellName { get; set; }

    [JsonIgnore]
    public string? Error { get; set; }

    [JsonIgnore]
    public string? Warning { get; set; }
}
