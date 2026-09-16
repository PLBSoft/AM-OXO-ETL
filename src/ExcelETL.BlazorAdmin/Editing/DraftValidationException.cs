namespace ExcelETL.BlazorAdmin.Editing;

// A validation failure found while converting a draft that no Domain constructor reports -- e.g. an
// absolute Excel range that doesn't parse (BlockFieldRangeFormatter), or an unconditional Colonne
// name left blank (the Domain doesn't validate that list). Lot 075
// (docs/tickets/tickets-tdd-lot-075-migration-editeur-import-brouillon.md). Carries a
// BlazorAdminMessages resource key rather than a message, so the mappers stay free of any localizer,
// same as for Domain exceptions (see ConversionResult<T>): the page resolves ResourceKey itself.
public sealed class DraftValidationException(string resourceKey)
    : Exception($"Draft validation failed: {resourceKey}")
{
    public string ResourceKey { get; } = resourceKey;
}
