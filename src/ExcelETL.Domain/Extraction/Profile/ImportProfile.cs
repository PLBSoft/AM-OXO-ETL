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

    // The Equipement parent's TypeElement.Nom for this profile (e.g. "MAD TRAVAUX" for a MAD dossier,
    // an as-yet-unconfirmed value for a future REL profile) -- added in model doc v2 after the client
    // clarified this value varies by profile and was never really the constant "MAD" originally
    // assumed. Never hardcoded in an extraction service; always read from here.
    public string EquipementTypeElementNom { get; }

    public IReadOnlyList<SheetExtractionRule> SheetRules { get; }

    public ImportProfile(string name, string equipementTypeElementNom, IReadOnlyList<SheetExtractionRule> sheetRules)
        : this(name, DefaultReperePrefix, equipementTypeElementNom, sheetRules)
    {
    }

    public ImportProfile(
        string name, string reperePrefix, string equipementTypeElementNom, IReadOnlyList<SheetExtractionRule> sheetRules)
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

        if (string.IsNullOrWhiteSpace(equipementTypeElementNom))
        {
            throw new DomainValidationException(
                "Equipement type element nom must not be empty.", nameof(equipementTypeElementNom),
                DomainErrorCode.ImportProfile_EmptyEquipementTypeElementNom);
        }

        ArgumentNullException.ThrowIfNull(sheetRules);

        if (sheetRules.Count == 0)
        {
            throw new DomainValidationException(
                "Sheet rules must contain at least one rule.", nameof(sheetRules), DomainErrorCode.ImportProfile_NoSheetRules);
        }

        Name = name;
        ReperePrefix = reperePrefix;
        EquipementTypeElementNom = equipementTypeElementNom;
        SheetRules = sheetRules;
    }
}
