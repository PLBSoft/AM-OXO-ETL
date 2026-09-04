using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Domain.Extraction.Profile;

// One sheet's extraction configuration within an ImportProfile. No identity of its own -- it's a
// configuration value owned by its ImportProfile, not an aggregate root. An empty PointRules list is
// a valid, meaningful state (means "always create the Point") -- see
// docs/modele-domaine-import-profile-2026-07-16.md §1.4.
//
// UnconditionalColonneNames resolves the gap PointRules alone can't express: a sheet's Colonnes are a
// mix of ones with a condition attached (represented as ConditionalPointRule entries grouped by
// ColonneName) and ones created for every extracted row with no condition at all (e.g. ISOLEMENT's
// "PROLOCK VANNES"/"DEPROLOCK VANNES") -- the latter have no natural representation as
// ConditionalPointRule instances (that type always carries a real SourceFieldName/ComparisonValue,
// there's no "no condition" sentinel). An empty list here is valid (e.g. DIVERS, where every Colonne
// is conditional).
public sealed class SheetExtractionRule
{
    // See RepeatingBlockLocator.Fields for why PointRules needs a backing field instead of a plain
    // auto-property: EF Core cannot constructor-bind an entity-collection navigation.
    // UnconditionalColonneNames doesn't need this treatment -- it's a primitive (string) collection,
    // not a navigation to an owned entity type, so EF Core binds it via the constructor like any
    // other scalar-ish property. HeaderFields/HeaderComposites (Lot 047) and FieldPresencePointRules
    // (PLATINES client feedback, 2026-09) need the same backing-field treatment as PointRules -- all
    // are navigations to owned entity types.
    private readonly List<ConditionalPointRule> _pointRules = [];
    private readonly List<HeaderFieldRule> _headerFields = [];
    private readonly List<HeaderCompositeRule> _headerComposites = [];
    private readonly List<FieldPresencePointRule> _fieldPresencePointRules = [];

    public string SheetName { get; }
    public RepeatingBlockLocator Locator { get; }
    public IReadOnlyList<ConditionalPointRule> PointRules => _pointRules;
    public IReadOnlyList<string> UnconditionalColonneNames { get; }
    public IReadOnlyList<HeaderFieldRule> HeaderFields => _headerFields;
    public IReadOnlyList<HeaderCompositeRule> HeaderComposites => _headerComposites;

    // A Point is created for FieldPresencePointRule.ColonneName only when its Cell is non-blank for
    // the current block -- unlike PointRules/UnconditionalColonneNames, which never look at whether a
    // specific cell (beyond the sheet's own declared, required fields) has a value. An empty list
    // (default) means "no field-presence rule for this sheet" -- every sheet other than PLATINES
    // today, and any PLATINES profile predating this feature.
    public IReadOnlyList<FieldPresencePointRule> FieldPresencePointRules => _fieldPresencePointRules;

    // Lot 063: the text a bloc's dedicated "zero energie" column must equal for ISOLEMENT's PS941
    // rule to consider it matched -- a field dedicated to this one sheet's own rule, not a generic
    // reusable mechanism (no other sheet has this notion). null = no such field configured for this
    // sheet (every sheet other than ISOLEMENT, and any ISOLEMENT profile predating this lot).
    public string? ZeroEnergieExpectedValue { get; }

    public SheetExtractionRule(
        string sheetName,
        RepeatingBlockLocator locator,
        IReadOnlyList<ConditionalPointRule> pointRules,
        IReadOnlyList<string> unconditionalColonneNames,
        IReadOnlyList<HeaderFieldRule> headerFields,
        IReadOnlyList<HeaderCompositeRule> headerComposites,
        string? zeroEnergieExpectedValue = null,
        IReadOnlyList<FieldPresencePointRule>? fieldPresencePointRules = null)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            throw new DomainValidationException(
                "Sheet name must not be empty.", nameof(sheetName), DomainErrorCode.SheetExtractionRule_EmptySheetName);
        }

        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(pointRules);
        ArgumentNullException.ThrowIfNull(unconditionalColonneNames);
        ArgumentNullException.ThrowIfNull(headerFields);
        ArgumentNullException.ThrowIfNull(headerComposites);

        if (zeroEnergieExpectedValue is not null && string.IsNullOrWhiteSpace(zeroEnergieExpectedValue))
        {
            throw new DomainValidationException(
                "Zero energie expected value must not be blank when provided.", nameof(zeroEnergieExpectedValue),
                DomainErrorCode.SheetExtractionRule_BlankZeroEnergieExpectedValue);
        }

        if (sheetName != locator.Sheet)
        {
            throw new DomainRuleViolationException(
                $"Sheet name '{sheetName}' must match the locator's sheet '{locator.Sheet}'.",
                DomainErrorCode.SheetExtractionRule_SheetNameLocatorMismatch,
                sheetName, locator.Sheet);
        }

        var headerFieldNames = headerFields.Select(f => f.Name).ToHashSet();
        foreach (var composite in headerComposites)
        {
            foreach (var placeholder in composite.PlaceholderNames())
            {
                if (!headerFieldNames.Contains(placeholder))
                {
                    throw new DomainRuleViolationException(
                        $"Header composite rule '{composite.Name}' references unknown placeholder '{{{placeholder}}}' " +
                        $"-- no header field rule named '{placeholder}' exists on this sheet.",
                        DomainErrorCode.SheetExtractionRule_HeaderCompositeReferencesUnknownField,
                        composite.Name, placeholder);
                }
            }
        }

        SheetName = sheetName;
        Locator = locator;
        _pointRules = [.. pointRules];
        UnconditionalColonneNames = unconditionalColonneNames;
        _headerFields = [.. headerFields];
        _headerComposites = [.. headerComposites];
        _fieldPresencePointRules = fieldPresencePointRules is null ? [] : [.. fieldPresencePointRules];
        ZeroEnergieExpectedValue = zeroEnergieExpectedValue;
    }

    // EF Core materialization only -- every property is set directly via reflection immediately
    // afterwards, bypassing this constructor's (nonexistent) validation entirely.
    private SheetExtractionRule()
    {
        SheetName = string.Empty;
        Locator = null!;
        UnconditionalColonneNames = [];
    }
}
