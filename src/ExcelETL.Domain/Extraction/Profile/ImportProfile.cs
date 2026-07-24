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
    public const int MaxNameLength = 60;

    // See RepeatingBlockLocator.Fields (Extraction/Primitives) for why SheetRules needs a backing
    // field instead of a plain auto-property: EF Core cannot constructor-bind an entity-collection
    // navigation.
    private readonly List<SheetExtractionRule> _sheetRules = [];

    public string Name { get; }
    public string ReperePrefix { get; }

    // The Equipement parent's TypeElement.Nom for this profile (e.g. "MAD TRAVAUX" for a MAD dossier,
    // an as-yet-unconfirmed value for a future REL profile) -- added in model doc v2 after the client
    // clarified this value varies by profile and was never really the constant "MAD" originally
    // assumed. Never hardcoded in an extraction service; always read from here.
    public string EquipementTypeElementNom { get; }

    // The Tableau names (e.g. "TRAVAUX COMPLET"/"TRAVAUX DETAIL") and Application names (e.g.
    // "PROGRESS", legacy EF6 AMProgress BaseElement<->Application many-to-many, kept as plain names
    // rather than a new entity -- see Lot U ticket doc) broadcast to the Equipement and every
    // Isolement of a run, same mechanism as loc1 (see ImportPipelineOrchestrator). Never a hardcoded
    // constant in an extraction service -- always read from here, same anti-hardcoding rule as
    // EquipementTypeElementNom.
    public IReadOnlyList<string> DefaultTableaux { get; }
    public IReadOnlyList<string> DefaultApplicationNames { get; }

    public IReadOnlyList<SheetExtractionRule> SheetRules => _sheetRules;

    public ImportProfile(
        string name, string equipementTypeElementNom,
        IReadOnlyList<string> defaultTableaux, IReadOnlyList<string> defaultApplicationNames,
        IReadOnlyList<SheetExtractionRule> sheetRules)
        : this(name, DefaultReperePrefix, equipementTypeElementNom, defaultTableaux, defaultApplicationNames, sheetRules)
    {
    }

    public ImportProfile(
        string name, string reperePrefix, string equipementTypeElementNom,
        IReadOnlyList<string> defaultTableaux, IReadOnlyList<string> defaultApplicationNames,
        IReadOnlyList<SheetExtractionRule> sheetRules)
        : this(Guid.NewGuid(), name, reperePrefix, equipementTypeElementNom, defaultTableaux, defaultApplicationNames, sheetRules)
    {
    }

    // Reconstructs an existing profile under its original Id. ImportProfile has no in-place mutation
    // methods -- editing a profile means building a brand new instance with the desired content and
    // handing it to IImportProfileStore.SaveAsync under the same Id as the profile it replaces, so the
    // store updates the existing row instead of inserting a duplicate. See EfImportProfileStore.
    public ImportProfile(
        Guid id, string name, string reperePrefix, string equipementTypeElementNom,
        IReadOnlyList<string> defaultTableaux, IReadOnlyList<string> defaultApplicationNames,
        IReadOnlyList<SheetExtractionRule> sheetRules)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(
                "Name must not be empty.", nameof(name), DomainErrorCode.ImportProfile_EmptyName);
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new DomainValidationException(
                $"Name must not exceed {MaxNameLength} characters.", nameof(name), DomainErrorCode.ImportProfile_NameTooLong,
                MaxNameLength);
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

        ArgumentNullException.ThrowIfNull(defaultTableaux);
        ArgumentNullException.ThrowIfNull(defaultApplicationNames);
        ArgumentNullException.ThrowIfNull(sheetRules);

        if (sheetRules.Count == 0)
        {
            throw new DomainValidationException(
                "Sheet rules must contain at least one rule.", nameof(sheetRules), DomainErrorCode.ImportProfile_NoSheetRules);
        }

        Name = name;
        ReperePrefix = reperePrefix;
        EquipementTypeElementNom = equipementTypeElementNom;
        DefaultTableaux = [.. defaultTableaux];
        DefaultApplicationNames = [.. defaultApplicationNames];
        _sheetRules = [.. sheetRules];
    }

    // EF Core materialization only -- every property is set directly via reflection immediately
    // afterwards, bypassing this constructor's (nonexistent) validation entirely.
    private ImportProfile()
    {
        Name = string.Empty;
        ReperePrefix = string.Empty;
        EquipementTypeElementNom = string.Empty;
        DefaultTableaux = [];
        DefaultApplicationNames = [];
    }
}
