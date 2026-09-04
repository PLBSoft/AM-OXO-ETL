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
    public const int MaxNameLength = ProfileNaming.MaxNameLength;

    // Distinct from MaxNameLength (the profile's own Name, 60 chars) -- this bounds one element of
    // DefaultTableaux/DefaultApplicationNames.
    public const int MaxListItemNameLength = 50;

    // See RepeatingBlockLocator.Fields (Extraction/Primitives) for why SheetRules needs a backing
    // field instead of a plain auto-property: EF Core cannot constructor-bind an entity-collection
    // navigation. TacheMultipleTypeLabels (Lot 067) needs the same treatment for the same reason.
    private readonly List<SheetExtractionRule> _sheetRules = [];
    private readonly List<TacheMultipleTypeLabel> _tacheMultipleTypeLabels = [];

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

    // Lot 067: a Code -> Label mapping broadcast onto every TacheMultiplePivot of a run (see
    // ImportPipelineOrchestrator), resolving TacheMultipleColonneTravaux for export. Deliberately
    // optional (last parameter, default null -> []) unlike DefaultTableaux/DefaultApplicationNames
    // above -- those are required with no default specifically because an accidental empty value would
    // silently break a load-bearing extraction behavior (PROCEDURE's own unconditional Points); an
    // empty TacheMultipleTypeLabels list is instead a legitimate, client-confirmed default (every
    // "Colonne Travaux" cell stays blank), so making it required would have forced touching ~72
    // pre-existing call sites for no safety benefit. See the ticket doc's decision 5.
    public IReadOnlyList<TacheMultipleTypeLabel> TacheMultipleTypeLabels => _tacheMultipleTypeLabels;

    public IReadOnlyList<SheetExtractionRule> SheetRules => _sheetRules;

    public ImportProfile(
        string name, string equipementTypeElementNom,
        IReadOnlyList<string> defaultTableaux, IReadOnlyList<string> defaultApplicationNames,
        IReadOnlyList<SheetExtractionRule> sheetRules,
        IReadOnlyList<TacheMultipleTypeLabel>? tacheMultipleTypeLabels = null)
        : this(
            name, DefaultReperePrefix, equipementTypeElementNom, defaultTableaux, defaultApplicationNames, sheetRules,
            tacheMultipleTypeLabels)
    {
    }

    public ImportProfile(
        string name, string reperePrefix, string equipementTypeElementNom,
        IReadOnlyList<string> defaultTableaux, IReadOnlyList<string> defaultApplicationNames,
        IReadOnlyList<SheetExtractionRule> sheetRules,
        IReadOnlyList<TacheMultipleTypeLabel>? tacheMultipleTypeLabels = null)
        : this(
            Guid.NewGuid(), name, reperePrefix, equipementTypeElementNom, defaultTableaux, defaultApplicationNames, sheetRules,
            tacheMultipleTypeLabels)
    {
    }

    // Reconstructs an existing profile under its original Id. ImportProfile has no in-place mutation
    // methods -- editing a profile means building a brand new instance with the desired content and
    // handing it to IImportProfileStore.SaveAsync under the same Id as the profile it replaces, so the
    // store updates the existing row instead of inserting a duplicate. See EfImportProfileStore.
    public ImportProfile(
        Guid id, string name, string reperePrefix, string equipementTypeElementNom,
        IReadOnlyList<string> defaultTableaux, IReadOnlyList<string> defaultApplicationNames,
        IReadOnlyList<SheetExtractionRule> sheetRules,
        IReadOnlyList<TacheMultipleTypeLabel>? tacheMultipleTypeLabels = null)
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

        var trimmedTableaux = new List<string>();
        foreach (var tableau in defaultTableaux)
        {
            ValidateDefaultTableauName(tableau, trimmedTableaux);
            trimmedTableaux.Add(tableau.Trim());
        }

        var trimmedApplicationNames = new List<string>();
        foreach (var applicationName in defaultApplicationNames)
        {
            ValidateDefaultApplicationName(applicationName, trimmedApplicationNames);
            trimmedApplicationNames.Add(applicationName.Trim());
        }

        var trimmedTacheMultipleTypeLabels = new List<TacheMultipleTypeLabel>();
        foreach (var label in tacheMultipleTypeLabels ?? [])
        {
            ArgumentNullException.ThrowIfNull(label);
            ValidateTacheMultipleTypeLabelCode(label, trimmedTacheMultipleTypeLabels);
            trimmedTacheMultipleTypeLabels.Add(new TacheMultipleTypeLabel(label.Code.Trim(), label.Label.Trim()));
        }

        Name = name;
        ReperePrefix = reperePrefix;
        EquipementTypeElementNom = equipementTypeElementNom;
        DefaultTableaux = trimmedTableaux;
        DefaultApplicationNames = trimmedApplicationNames;
        _sheetRules = [.. sheetRules];
        _tacheMultipleTypeLabels = trimmedTacheMultipleTypeLabels;
    }

    // The single validation path for one candidate Tableau name against the (already-trimmed) names
    // already accepted into its own list -- called both by this constructor (in a loop) and by
    // ImportProfileEditor.razor's own add/edit-in-line UI (Lot 059), so the two never drift into two
    // separate notions of "valid".
    public static void ValidateDefaultTableauName(string candidate, IReadOnlyList<string> existing) =>
        ValidateListItemName(
            candidate, existing,
            DomainErrorCode.ImportProfile_EmptyTableauName,
            DomainErrorCode.ImportProfile_TableauNameTooLong,
            DomainErrorCode.ImportProfile_DuplicateTableauName);

    public static void ValidateDefaultApplicationName(string candidate, IReadOnlyList<string> existing) =>
        ValidateListItemName(
            candidate, existing,
            DomainErrorCode.ImportProfile_EmptyApplicationName,
            DomainErrorCode.ImportProfile_ApplicationNameTooLong,
            DomainErrorCode.ImportProfile_DuplicateApplicationName);

    // Lot 067: the single validation path for one candidate TacheMultipleTypeLabel's Code against the
    // Codes already accepted into its own list -- called both by this constructor (in a loop, above)
    // and by ImportProfileEditor.razor's own add/edit-in-line UI, so the two never drift into two
    // separate notions of "valid". Empty/too-long Code or Label is already rejected by
    // TacheMultipleTypeLabel's own constructor before this ever runs -- this method only adds the
    // list-level duplicate-Code check that a lone value object has no way to enforce on itself.
    public static void ValidateTacheMultipleTypeLabelCode(
        TacheMultipleTypeLabel candidate, IReadOnlyList<TacheMultipleTypeLabel> existing)
    {
        var trimmedCode = candidate.Code.Trim();

        if (existing.Any(l => string.Equals(l.Code.Trim(), trimmedCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainValidationException(
                $"Code '{trimmedCode}' already exists.", nameof(candidate),
                DomainErrorCode.ImportProfile_DuplicateTacheMultipleTypeLabelCode, trimmedCode);
        }
    }

    private static void ValidateListItemName(
        string candidate, IReadOnlyList<string> existing,
        DomainErrorCode emptyCode, DomainErrorCode tooLongCode, DomainErrorCode duplicateCode)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new DomainValidationException("Name must not be empty.", nameof(candidate), emptyCode);
        }

        var trimmed = candidate.Trim();

        if (trimmed.Length > MaxListItemNameLength)
        {
            throw new DomainValidationException(
                $"Name must not exceed {MaxListItemNameLength} characters.", nameof(candidate), tooLongCode,
                MaxListItemNameLength);
        }

        if (existing.Any(e => string.Equals(e.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainValidationException(
                $"Name '{trimmed}' already exists.", nameof(candidate), duplicateCode, trimmed);
        }
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
