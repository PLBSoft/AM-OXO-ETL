using ExcelETL.Application.Extraction.Oxo.Elements;
using ExcelETL.BlazorAdmin.Formatting;

namespace ExcelETL.BlazorAdmin.Editing.Import;

// What the import editor warns about, from the same table as the Details page (ImportSheetUsage, itself
// checked against the real pipeline). Since lot 084 the five element sheets share one engine that reads
// every setting the editor offers, so only three things are left to report:
// - a sheet name the import doesn't process;
// - what PROCEDURE ignores: its configured stop field (it always stops on Action) and colour settings;
// - colour combinations with no effect: a default next to a "CouleurEtiquette" block field (the field
//   wins), or an allowed list without that field (nothing to filter).
// Every flag is only raised once the user has entered something.
public sealed record SheetRuleIgnoredSettings(
    bool SheetNotProcessed,
    string? IgnoredStopFieldFixedName,
    bool CouleurEtiquetteIgnored,
    bool DefaultCouleurIgnoredBecauseCell,
    bool AllowedCouleursIgnoredWithoutCell)
{
    public static SheetRuleIgnoredSettings None { get; } = new(false, null, false, false, false);

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

        static bool Filled(string value) => !string.IsNullOrWhiteSpace(value);

        var stopField = rule.StopFieldName.Trim();
        var ignoredStopFieldFixedName = usage.FixedStopFieldName is { } fixedName && stopField.Length > 0 && stopField != fixedName
            ? fixedName
            : null;

        var readsCouleur = usage.ReadMembers.Contains(SheetRuleMember.CouleurEtiquette);
        var hasCouleurField = rule.Fields.Append(rule.PendingField)
            .Any(f => f.Name.Trim() == ElementFieldNames.CouleurEtiquette);
        var couleurIgnored = !readsCouleur && (Filled(rule.DefaultCouleurEtiquette) || Filled(rule.AllowedCouleursEtiquette));
        var defaultIgnoredBecauseCell = readsCouleur && hasCouleurField && Filled(rule.DefaultCouleurEtiquette);
        var allowedIgnoredWithoutCell = readsCouleur && !hasCouleurField && Filled(rule.AllowedCouleursEtiquette);

        return new SheetRuleIgnoredSettings(
            SheetNotProcessed: false, ignoredStopFieldFixedName, couleurIgnored, defaultIgnoredBecauseCell, allowedIgnoredWithoutCell)
        {
            AnyConfiguredSettingIgnored = ignoredStopFieldFixedName is not null
                || couleurIgnored || defaultIgnoredBecauseCell || allowedIgnoredWithoutCell
        };
    }
}
