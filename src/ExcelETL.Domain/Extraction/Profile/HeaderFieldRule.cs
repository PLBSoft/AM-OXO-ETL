using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Domain.Extraction.Profile;

// One directly-read header cell (Lot 047, spec-migration-entetes-profile-driven-directcell.md §3).
// Name is the logical identifier a HeaderCompositeRule.Template placeholder ({name}) references -- it
// is not tied to any Pivot field name. StripReperePrefix/DateFormat are deliberately the only two
// transformations offered: a flat, non-recursive model (unlike TextTransform), chosen specifically so
// it stays trivially persistable and editable from the Blazor profile editor (Lot 048).
public sealed record HeaderFieldRule
{
    public string Name { get; }
    public DirectCell Cell { get; }
    public bool StripReperePrefix { get; }
    public string? DateFormat { get; }

    public HeaderFieldRule(string name, DirectCell cell, bool stripReperePrefix = false, string? dateFormat = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(
                "Name must not be empty.", nameof(name), DomainErrorCode.HeaderFieldRule_EmptyName);
        }

        ArgumentNullException.ThrowIfNull(cell);

        if (dateFormat is not null && string.IsNullOrWhiteSpace(dateFormat))
        {
            throw new DomainValidationException(
                "Date format must not be blank when provided.", nameof(dateFormat),
                DomainErrorCode.HeaderFieldRule_BlankDateFormat);
        }

        Name = name;
        Cell = cell;
        StripReperePrefix = stripReperePrefix;
        DateFormat = dateFormat;
    }

    // EF Core materialization only -- constructor binding cannot bind a reference to an owned type
    // (confirmed empirically, same "Navigations... cannot be bound" restriction RepeatingBlockLocator's
    // own comment documents for entity-collection navigations) any more than it can bind a collection
    // navigation. Cell is set directly via reflection immediately afterwards, bypassing this
    // constructor's (nonexistent) validation entirely.
    private HeaderFieldRule()
    {
        Name = string.Empty;
        Cell = null!;
    }
}
