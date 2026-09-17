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
                sections.Add(BuildSheetSection(rule, ImportSheetUsage.For(sheetName)!, profile, loc));
            }
        }

        return new ImportProfileDescription(sections);
    }

    private static ProfileDescriptionSection BuildSheetSection(
        SheetExtractionRule rule, ImportSheetUsageEntry usage, ImportProfile profile, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var sentences = new List<ProfileDescriptionSentence>();
        if (usage.ReadMembers.Contains(SheetRuleMember.HeaderRules))
        {
            sentences.AddRange(DescribeHeader(rule, usage, profile.ReperePrefix, loc));
        }

        sentences.AddRange(DescribeBlocks(rule.Locator, usage.ItemKind, loc));

        return new ProfileDescriptionSection(loc["ImportProfileDetails_SheetSectionTitle", rule.SheetName], sentences, [], []);
    }

    // Only what extraction uses: the header names the sheet requires, plus any field a used composite
    // references. Anything else is stored but unused (reported separately, D2).
    private static IEnumerable<ProfileDescriptionSentence> DescribeHeader(
        SheetExtractionRule rule, ImportSheetUsageEntry usage, string reperePrefix, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var usedComposites = rule.HeaderComposites
            .Where(c => usage.RequiredHeaderComposites.Any(required => required.Name == c.Name))
            .ToList();
        var referencedFieldNames = usedComposites.SelectMany(c => c.PlaceholderNames()).ToHashSet(StringComparer.Ordinal);

        foreach (var field in rule.HeaderFields)
        {
            var role = usage.RequiredHeaderFields.FirstOrDefault(required => required.Name == field.Name)?.Role;
            if (role is null && !referencedFieldNames.Contains(field.Name))
            {
                continue;
            }

            var options = "";
            if (field.Cell.Sheet != rule.SheetName)
            {
                options += loc["ImportProfileDetails_HeaderOptionOtherSheet", Quote(field.Cell.Sheet, loc)];
            }

            if (field.StripReperePrefix)
            {
                options += loc["ImportProfileDetails_HeaderOptionStripPrefix", Quote(reperePrefix, loc)];
            }

            if (field.DateFormat is not null)
            {
                options += loc["ImportProfileDetails_HeaderOptionDateFormat", Quote(field.DateFormat, loc)];
            }

            var key = role is null ? "ImportProfileDetails_HeaderField_Other" : $"ImportProfileDetails_HeaderField_{role}";
            yield return new(loc[key, Quote(field.Name, loc), field.Cell.Range, options]);
        }

        foreach (var composite in usedComposites)
        {
            var placeholders = composite.PlaceholderNames().Select(name => "{" + name + "}").ToList();
            yield return new(placeholders.Count switch
            {
                0 => loc["ImportProfileDetails_HeaderDesignationNoPlaceholder", Quote(composite.Template, loc)],
                1 => loc["ImportProfileDetails_HeaderDesignationOnePlaceholder", Quote(composite.Template, loc), placeholders[0]],
                _ => loc["ImportProfileDetails_HeaderDesignationSeveralPlaceholders", Quote(composite.Template, loc),
                    JoinWithAnd(placeholders, loc)],
            });
        }
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

    // "a", "a et b", "a, b et c".
    private static string JoinWithAnd(IReadOnlyList<string> values, IStringLocalizer<BlazorAdminMessages> loc) =>
        values.Count == 1
            ? values[0]
            : string.Join(ListSeparator, values.Take(values.Count - 1)) + loc["ImportProfileDetails_ListLastSeparator"] + values[^1];

    private static string QuoteList(IEnumerable<string> values, IStringLocalizer<BlazorAdminMessages> loc) =>
        string.Join(ListSeparator, values.Select(value => Quote(value, loc)));
}
