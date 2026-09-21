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
    // other scalar-ish property. HeaderFields/HeaderComposites (Lot 047) need the same backing-field
    // treatment as PointRules -- all are navigations to owned entity types.
    private readonly List<ConditionalPointRule> _pointRules = [];
    private readonly List<HeaderFieldRule> _headerFields = [];
    private readonly List<HeaderCompositeRule> _headerComposites = [];

    public string SheetName { get; }
    public RepeatingBlockLocator Locator { get; }
    public IReadOnlyList<ConditionalPointRule> PointRules => _pointRules;
    public IReadOnlyList<string> UnconditionalColonneNames { get; }
    public IReadOnlyList<HeaderFieldRule> HeaderFields => _headerFields;
    public IReadOnlyList<HeaderCompositeRule> HeaderComposites => _headerComposites;

    // Client feedback (2026-09): some sheets never carry a per-block cell at all -- every element on
    // that sheet gets the exact same fixed value (e.g. AUTRES JOINTS TOUCHES is always "BLEUE"). Since
    // lot 084 (G10) the per-block colour is the optional block field "CouleurEtiquette"; when a sheet
    // declares it, that field wins over this default. null = no fixed value configured for this sheet.
    public string? DefaultCouleurEtiquette { get; }

    // Client feedback (2026-09-11): a whitelist approach, replacing the earlier hardcoded "DATE"
    // blacklist entry -- a value read from the "CouleurEtiquette" block field that matches none of these (trim +
    // case-insensitive, same §7 convention) is a non-blocking UnexpectedCouleurEtiquetteValue warning
    // rather than being silently imported or silently dropped. null = no restriction configured for
    // this sheet (opt-in feature, backward compatible with any profile predating it -- the cell's raw
    // content is then accepted as-is, exactly the pre-whitelist behavior). Deliberately never applied
    // to DefaultCouleurEtiquette -- that value is admin-typed profile configuration, not read from an
    // unreliable client cell, so it carries no equivalent data-quality risk to warn about.
    public IReadOnlyList<string>? AllowedCouleursEtiquette { get; }

    // Lot 084 (G3): whether an element for which no conditional Colonne got ticked produces a
    // NoConditionalPointCreated warning. Off by default; the standard profile turns it on for
    // ISOLEMENT, AUTRES JOINTS TOUCHES and DIVERS (their behavior before the lot).
    public bool WarnWhenNoConditionalPoint { get; }

    public SheetExtractionRule(
        string sheetName,
        RepeatingBlockLocator locator,
        IReadOnlyList<ConditionalPointRule> pointRules,
        IReadOnlyList<string> unconditionalColonneNames,
        IReadOnlyList<HeaderFieldRule> headerFields,
        IReadOnlyList<HeaderCompositeRule> headerComposites,
        string? defaultCouleurEtiquette = null,
        IReadOnlyList<string>? allowedCouleursEtiquette = null,
        bool warnWhenNoConditionalPoint = false)
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

        if (defaultCouleurEtiquette is not null && string.IsNullOrWhiteSpace(defaultCouleurEtiquette))
        {
            throw new DomainValidationException(
                "Default couleur etiquette must not be blank when provided.", nameof(defaultCouleurEtiquette),
                DomainErrorCode.SheetExtractionRule_BlankDefaultCouleurEtiquette);
        }

        if (allowedCouleursEtiquette is not null && allowedCouleursEtiquette.Any(string.IsNullOrWhiteSpace))
        {
            throw new DomainValidationException(
                "Allowed couleurs etiquette must not contain a blank entry.", nameof(allowedCouleursEtiquette),
                DomainErrorCode.SheetExtractionRule_BlankAllowedCouleurEtiquette);
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

        // Lot 084: a point rule can only read a field of its own block -- a name outside it used to
        // fail the whole import at extraction time (UnknownFieldReferenceException).
        var blockFieldNames = locator.Fields.Select(f => f.Name).ToHashSet();
        foreach (var pointRule in pointRules)
        {
            if (!blockFieldNames.Contains(pointRule.SourceFieldName))
            {
                throw new DomainRuleViolationException(
                    $"Point rule for Colonne '{pointRule.ColonneName}' references '{pointRule.SourceFieldName}', " +
                    "which is not a field of this sheet's block.",
                    DomainErrorCode.SheetExtractionRule_PointRuleReferencesUnknownBlockField,
                    pointRule.ColonneName, pointRule.SourceFieldName);
            }
        }

        SheetName = sheetName;
        Locator = locator;
        _pointRules = [.. pointRules];
        UnconditionalColonneNames = unconditionalColonneNames;
        _headerFields = [.. headerFields];
        _headerComposites = [.. headerComposites];
        DefaultCouleurEtiquette = defaultCouleurEtiquette;
        AllowedCouleursEtiquette = allowedCouleursEtiquette;
        WarnWhenNoConditionalPoint = warnWhenNoConditionalPoint;
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
