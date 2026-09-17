using ExcelETL.BlazorAdmin.Resources;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.Extensions.Localization;

namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 078 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md): turns an
// ImportProfile into plain-language sentences for a user who is neither a developer nor an expert of
// the application. Presentation only: no rule is rebuilt or validated here. Profile values (column
// names, compared values) are data and are never translated; only the sentence templates come from
// the .resx (ImportProfileDetails_* keys, French text in both files for now -- decision D5).
public static class ImportProfileDescriptionBuilder
{
    private const string ListSeparator = ", ";

    public static ImportProfileDescription Build(ImportProfile profile, IStringLocalizer<BlazorAdminMessages> loc)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(loc);

        var sections = new List<ProfileDescriptionSection> { BuildGeneralSection(profile, loc) };

        // Pipeline order, never the profile's own SheetRules order (not guaranteed after an EF round trip).
        // Like ImportPipelineOrchestrator.FindRule, only the first rule of a given name is processed.
        foreach (var sheetName in ImportSheetUsage.KnownSheetNames)
        {
            var rule = profile.SheetRules.FirstOrDefault(r => r.SheetName == sheetName);
            if (rule is not null)
            {
                sections.Add(BuildSheetSection(rule, ImportSheetUsage.For(sheetName)!, loc));
            }
        }

        return new ImportProfileDescription(sections);
    }

    private static ProfileDescriptionSection BuildSheetSection(
        SheetExtractionRule rule, ImportSheetUsageEntry usage, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var sentences = new List<ProfileDescriptionSentence>();
        sentences.AddRange(DescribeBlocks(rule.Locator, usage.ItemKind, loc));

        return new ProfileDescriptionSection(loc["ImportProfileDetails_SheetSectionTitle", rule.SheetName], sentences, [], []);
    }

    // D1: where the data is read -- step, start row, stop field, then each field's range in the first block.
    private static IEnumerable<ProfileDescriptionSentence> DescribeBlocks(
        RepeatingBlockLocator locator, BlockItemKind itemKind, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var stopField = FieldLabel(locator.StopFieldName, definite: true, loc);
        var isTask = itemKind == BlockItemKind.Task;

        yield return new(locator.Step == 1
            ? loc[isTask ? "ImportProfileDetails_BlockTaskPerLine" : "ImportProfileDetails_BlockElementPerLine",
                locator.FirstBlockStartRow, stopField]
            : loc[isTask ? "ImportProfileDetails_BlockTaskEveryNLines" : "ImportProfileDetails_BlockElementEveryNLines",
                locator.Step, locator.FirstBlockStartRow, stopField]);

        var fields = string.Join(ListSeparator, locator.Fields.Select(field => loc[
            "ImportProfileDetails_BlockFieldAt",
            FieldLabel(field.Name, definite: false, loc),
            BlockFieldRangeFormatter.ToAbsoluteRange(
                locator.FirstBlockStartRow, field.ColumnRange, field.RowOffsetStart, field.RowOffsetEnd)].Value));

        yield return new(loc[isTask ? "ImportProfileDetails_BlockFirstTaskFields" : "ImportProfileDetails_BlockFirstElementFields", fields]);
    }

    // Known technical field names get a business label; any other name (typed by an admin) is shown quoted.
    private static string FieldLabel(string fieldName, bool definite, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var prefix = definite ? "ImportProfileDetails_FieldLabelDefinite_" : "ImportProfileDetails_FieldLabel_";
        var known = loc[prefix + fieldName];
        return known.ResourceNotFound ? loc[prefix + "Unknown", Quote(fieldName, loc)] : known;
    }

    private static ProfileDescriptionSection BuildGeneralSection(ImportProfile profile, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var sentences = new List<ProfileDescriptionSentence>
        {
            new(loc["ImportProfileDetails_GeneralReperePrefix", Quote(profile.ReperePrefix, loc)]),
            new(loc["ImportProfileDetails_GeneralEquipementTypeElement", Quote(profile.EquipementTypeElementNom, loc)]),
            new(CountedSentence(
                profile.DefaultTableaux, loc, "ImportProfileDetails_GeneralNoTableau",
                "ImportProfileDetails_GeneralOneTableau", "ImportProfileDetails_GeneralSeveralTableaux")),
            new(CountedSentence(
                profile.DefaultApplicationNames, loc, "ImportProfileDetails_GeneralNoApplication",
                "ImportProfileDetails_GeneralOneApplication", "ImportProfileDetails_GeneralSeveralApplications")),
        };

        sentences.AddRange(profile.TacheMultipleTypeLabels.Select(label => new ProfileDescriptionSentence(
            loc["ImportProfileDetails_GeneralTacheMultipleTypeLabel", Quote(label.Code, loc), Quote(label.Label, loc)])));

        return new ProfileDescriptionSection(loc["ImportProfileDetails_GeneralSectionTitle"], sentences, [], []);
    }

    // Zero / one / several: the "several" template takes the count as {0} and the quoted list as {1}.
    private static string CountedSentence(
        IReadOnlyList<string> values, IStringLocalizer<BlazorAdminMessages> loc, string noneKey, string oneKey, string severalKey) =>
        values.Count switch
        {
            0 => loc[noneKey],
            1 => loc[oneKey, Quote(values[0], loc)],
            _ => loc[severalKey, values.Count, QuoteList(values, loc)],
        };

    private static string Quote(string value, IStringLocalizer<BlazorAdminMessages> loc) =>
        loc["ImportProfileDetails_QuotedValue", value];

    private static string QuoteList(IEnumerable<string> values, IStringLocalizer<BlazorAdminMessages> loc) =>
        string.Join(ListSeparator, values.Select(value => Quote(value, loc)));
}
