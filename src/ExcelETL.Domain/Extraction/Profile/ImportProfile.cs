using ExcelETL.Domain.Common;
using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Profile;

// The aggregate root configuring one end-to-end extraction run (repère prefix + one rule per source
// sheet). EF Core persistence is deliberately deferred (see Lot E) -- for now this is meant to be
// constructed once, in-memory, with a hardcoded rule set, per
// docs/tickets-tdd-extraction-2026-07-16.md's proposed sequencing.
public sealed class ImportProfile : Entity
{
    public const string DefaultReperePrefix = "MAD-OXO-";

    public string Name { get; }
    public string ReperePrefix { get; }
    public IReadOnlyList<SheetExtractionRule> SheetRules { get; }

    public ImportProfile(string name, IReadOnlyList<SheetExtractionRule> sheetRules)
        : this(name, DefaultReperePrefix, sheetRules)
    {
    }

    public ImportProfile(string name, string reperePrefix, IReadOnlyList<SheetExtractionRule> sheetRules)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(
                "Name must not be empty.", nameof(name), DomainErrorCode.ImportProfile_EmptyName);
        }

        if (string.IsNullOrWhiteSpace(reperePrefix))
        {
            throw new DomainValidationException(
                "Repere prefix must not be empty.", nameof(reperePrefix), DomainErrorCode.ImportProfile_EmptyReperePrefix);
        }

        ArgumentNullException.ThrowIfNull(sheetRules);

        if (sheetRules.Count == 0)
        {
            throw new DomainValidationException(
                "Sheet rules must contain at least one rule.", nameof(sheetRules), DomainErrorCode.ImportProfile_NoSheetRules);
        }

        Name = name;
        ReperePrefix = reperePrefix;
        SheetRules = sheetRules;
    }
}
