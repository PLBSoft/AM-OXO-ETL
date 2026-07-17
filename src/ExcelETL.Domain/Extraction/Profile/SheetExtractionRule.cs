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
    public string SheetName { get; }
    public RepeatingBlockLocator Locator { get; }
    public IReadOnlyList<ConditionalPointRule> PointRules { get; }
    public IReadOnlyList<string> UnconditionalColonneNames { get; }

    public SheetExtractionRule(
        string sheetName,
        RepeatingBlockLocator locator,
        IReadOnlyList<ConditionalPointRule> pointRules,
        IReadOnlyList<string> unconditionalColonneNames)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            throw new DomainValidationException(
                "Sheet name must not be empty.", nameof(sheetName), DomainErrorCode.SheetExtractionRule_EmptySheetName);
        }

        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(pointRules);
        ArgumentNullException.ThrowIfNull(unconditionalColonneNames);

        if (sheetName != locator.Sheet)
        {
            throw new DomainRuleViolationException(
                $"Sheet name '{sheetName}' must match the locator's sheet '{locator.Sheet}'.",
                DomainErrorCode.SheetExtractionRule_SheetNameLocatorMismatch,
                sheetName, locator.Sheet);
        }

        SheetName = sheetName;
        Locator = locator;
        PointRules = pointRules;
        UnconditionalColonneNames = unconditionalColonneNames;
    }
}
