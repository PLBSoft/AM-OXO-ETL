using ExcelETL.Application.Extraction.Oxo.Elements;
using ExcelETL.BlazorAdmin.Formatting;

namespace ExcelETL.BlazorAdmin.Editing.Import;

// Client ticket J2M76 (2026-09-21): the import editor offers the same sections for every sheet, while each
// sheet's extraction only reads some of them (PLATINES applies no conditional point rule, ISOLEMENT reads
// no header...). This tells the editor what to warn about, from the same table as the Details page
// (ImportSheetUsage, itself checked against the real pipeline).
//
// The three section flags are true whenever the sheet doesn't read that section, even when it is empty, so
// the user is warned before filling it in. The other settings are only reported once they hold a value.
public sealed record SheetRuleIgnoredSettings(
    bool SheetNotProcessed,
    bool ConditionalPointRulesIgnored,
    bool FieldPresencePointRulesIgnored,
    bool HeaderRulesIgnored,
    string? IgnoredStopFieldFixedName,
    bool ZeroEnergieExpectedValueIgnored,
    bool CouleurEtiquetteIgnored,
    bool DefaultCouleurIgnoredBecauseCell,
    bool AllowedCouleursIgnoredWithoutCell,
    bool CouleurEtiquetteCellIgnored = false)
{
    public static SheetRuleIgnoredSettings None { get; } = new(false, false, false, false, null, false, false, false, false);

    // True when something the user actually entered has no effect -- the sheet-rule card's warning.
    public bool AnyConfiguredSettingIgnored { get; private init; }

    public static SheetRuleIgnoredSettings For(SheetExtractionRuleDraft rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (string.IsNullOrWhiteSpace(rule.SheetName))
        {
            return None;
        }

        var usage = ImportSheetUsage.For(rule.SheetName);
        if (usage is null)
        {
            return None with { SheetNotProcessed = true, AnyConfiguredSettingIgnored = true };
        }

        bool Ignores(SheetRuleMember member) => !usage.ReadMembers.Contains(member);
        static bool Filled(string value) => !string.IsNullOrWhiteSpace(value);

        var conditionalIgnored = Ignores(SheetRuleMember.ConditionalPointRules);
        var fieldPresenceIgnored = Ignores(SheetRuleMember.FieldPresencePointRules);
        var headerIgnored = Ignores(SheetRuleMember.HeaderRules);

        var stopField = rule.StopFieldName.Trim();
        var ignoredStopFieldFixedName = usage.FixedStopFieldName is { } fixedName && stopField.Length > 0 && stopField != fixedName
            ? fixedName
            : null;

        var zeroEnergieIgnored = Ignores(SheetRuleMember.ZeroEnergieExpectedValue) && Filled(rule.ZeroEnergieExpectedValue);

        // Lot 084 (G10): the couleur is read from a block field named "CouleurEtiquette"; the dedicated cell
        // is read by no sheet any more (removed in 84.8).
        var readsCouleur = !Ignores(SheetRuleMember.CouleurEtiquette);
        var hasCouleurField = rule.Fields.Append(rule.PendingField)
            .Any(f => f.Name.Trim() == ElementFieldNames.CouleurEtiquette);
        var couleurIgnored = !readsCouleur && (Filled(rule.DefaultCouleurEtiquette) || Filled(rule.AllowedCouleursEtiquette));
        var defaultIgnoredBecauseCell = readsCouleur && hasCouleurField && Filled(rule.DefaultCouleurEtiquette);
        var allowedIgnoredWithoutCell = readsCouleur && !hasCouleurField && Filled(rule.AllowedCouleursEtiquette);
        var couleurCellIgnored = Ignores(SheetRuleMember.CouleurEtiquetteCell) && Filled(rule.CouleurEtiquetteCellRange);

        var anyConfigured =
            (conditionalIgnored && rule.PointRules.Count > 0)
            || (fieldPresenceIgnored && rule.FieldPresencePointRules.Count > 0)
            || (headerIgnored && (rule.HeaderFields.Count > 0 || rule.HeaderComposites.Count > 0))
            || ignoredStopFieldFixedName is not null
            || zeroEnergieIgnored || couleurIgnored || defaultIgnoredBecauseCell || allowedIgnoredWithoutCell
            || couleurCellIgnored;

        return new SheetRuleIgnoredSettings(
            SheetNotProcessed: false, conditionalIgnored, fieldPresenceIgnored, headerIgnored, ignoredStopFieldFixedName,
            zeroEnergieIgnored, couleurIgnored, defaultIgnoredBecauseCell, allowedIgnoredWithoutCell, couleurCellIgnored)
        {
            AnyConfiguredSettingIgnored = anyConfigured
        };
    }
}
