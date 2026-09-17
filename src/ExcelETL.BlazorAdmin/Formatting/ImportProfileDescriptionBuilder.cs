using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.BlazorAdmin.Resources;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.Extensions.Localization;
using static ExcelETL.BlazorAdmin.Formatting.ProfileDescriptionMarking;

namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 078 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md): turns an
// ImportProfile into plain-language sentences for a user who is neither a developer nor an expert of
// the application. Presentation only: no rule is rebuilt or validated here. Profile values (column
// names, compared values) are data and are never translated; only the sentence templates come from
// the .resx (ImportProfileDetails_* keys, French text in both files for now -- decision D5).
public static class ImportProfileDescriptionBuilder
{
    public static ProfileDescription Build(ImportProfile profile, IStringLocalizer<BlazorAdminMessages> loc)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(loc);

        var sections = new List<ProfileDescriptionSection> { BuildGeneralSection(profile, loc) };
        var processedRules = new HashSet<SheetExtractionRule>(ReferenceEqualityComparer.Instance);

        // Pipeline order, never the profile's own SheetRules order (not guaranteed after an EF round trip).
        // Like ImportPipelineOrchestrator.FindRule, only the first rule of a given name is processed.
        foreach (var sheetName in ImportSheetUsage.KnownSheetNames)
        {
            var rule = profile.SheetRules.FirstOrDefault(r => r.SheetName == sheetName);
            if (rule is not null)
            {
                processedRules.Add(rule);
                sections.Add(BuildSheetSection(rule, ImportSheetUsage.For(sheetName)!, profile, loc));
            }
        }

        // Rules extraction never reaches: an unknown sheet name, or a second rule with a known one.
        foreach (var rule in profile.SheetRules.Where(r => !processedRules.Contains(r)))
        {
            var notice = ImportSheetUsage.IsProcessed(rule.SheetName)
                ? loc["ImportProfileDetails_IgnoredDuplicateSheet"]
                : loc["ImportProfileDetails_IgnoredUnprocessedSheet"];
            sections.Add(new ProfileDescriptionSection(
                loc["ImportProfileDetails_SheetSectionTitle", rule.SheetName], [], Marked([notice]), []));
        }

        return new ProfileDescription(sections);
    }

    private static ProfileDescriptionSection BuildSheetSection(
        SheetExtractionRule rule, ImportSheetUsageEntry usage, ImportProfile profile, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var sentences = new List<ProfileDescriptionSentence>();
        if (usage.ReadMembers.Contains(SheetRuleMember.HeaderRules))
        {
            sentences.AddRange(DescribeHeader(rule, usage, profile.ReperePrefix, loc));
        }

        sentences.AddRange(DescribeBlocks(rule.Locator, usage, loc));
        sentences.AddRange(usage.FixedBehaviors.Select(behavior =>
            new ProfileDescriptionSentence(loc[$"ImportProfileDetails_Fixed_{behavior.Kind}", behavior.Range is null ? "" : CellRef(behavior.Range)], IsFixed: true)));
        sentences.AddRange(DescribePoints(rule, usage, loc));
        if (usage.ReadMembers.Contains(SheetRuleMember.CouleurEtiquette))
        {
            sentences.AddRange(DescribeCouleur(rule, loc));
        }

        return new ProfileDescriptionSection(
            loc["ImportProfileDetails_SheetSectionTitle", rule.SheetName], sentences,
            Marked(DescribeIgnored(rule, usage, loc)), Marked(DescribeBlocking(rule, usage, loc)));
    }

    // Header rules extraction uses: the composites the sheet requires, and the fields those composites
    // reference (required fields are matched by name separately).
    private static (List<HeaderCompositeRule> UsedComposites, HashSet<string> ReferencedFieldNames) UsedHeaderRules(
        SheetExtractionRule rule, ImportSheetUsageEntry usage)
    {
        var usedComposites = rule.HeaderComposites
            .Where(c => usage.RequiredHeaderComposites.Any(required => required.Name == c.Name))
            .ToList();
        return (usedComposites, usedComposites.SelectMany(c => c.PlaceholderNames()).ToHashSet(StringComparer.Ordinal));
    }

    // D2: every setting stored in the rule that this sheet's extraction never uses.
    private static List<string> DescribeIgnored(
        SheetExtractionRule rule, ImportSheetUsageEntry usage, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var ignored = new List<string>();
        bool Reads(SheetRuleMember member) => usage.ReadMembers.Contains(member);

        if (usage.FixedStopFieldName is { } fixedStopField && rule.Locator.StopFieldName != fixedStopField)
        {
            ignored.Add(loc["ImportProfileDetails_IgnoredStopField", Quote(rule.Locator.StopFieldName, loc),
                FieldLabel(fixedStopField, definite: true, loc)]);
        }

        var readsHeader = Reads(SheetRuleMember.HeaderRules);
        var (usedComposites, referencedFieldNames) = readsHeader ? UsedHeaderRules(rule, usage) : ([], []);
        ignored.AddRange(rule.HeaderFields
            .Where(f => !readsHeader
                || (usage.RequiredHeaderFields.All(required => required.Name != f.Name) && !referencedFieldNames.Contains(f.Name)))
            .Select(f => loc["ImportProfileDetails_IgnoredHeaderField", Quote(f.Name, loc), CellRef(f.Cell.Range)].Value));
        ignored.AddRange(rule.HeaderComposites
            .Where(c => !usedComposites.Contains(c))
            .Select(c => loc["ImportProfileDetails_IgnoredHeaderComposite", Quote(c.Name, loc)].Value));

        if (!Reads(SheetRuleMember.UnconditionalColonnes))
        {
            ignored.AddRange(rule.UnconditionalColonneNames.Select(name =>
                loc["ImportProfileDetails_IgnoredUnconditionalColonne", Quote(name, loc)].Value));
        }

        if (!Reads(SheetRuleMember.ConditionalPointRules))
        {
            ignored.AddRange(rule.PointRules.Select(r =>
                loc["ImportProfileDetails_IgnoredConditionalRule", Quote(r.ColonneName, loc)].Value));
        }

        if (!Reads(SheetRuleMember.FieldPresencePointRules))
        {
            ignored.AddRange(rule.FieldPresencePointRules.Select(r =>
                loc["ImportProfileDetails_IgnoredFieldPresenceRule", Quote(r.ColonneName, loc)].Value));
        }

        if (!Reads(SheetRuleMember.ZeroEnergieExpectedValue) && rule.ZeroEnergieExpectedValue is not null)
        {
            ignored.Add(loc["ImportProfileDetails_IgnoredZeroEnergieExpectedValue", Quote(rule.ZeroEnergieExpectedValue, loc)]);
        }

        ignored.AddRange(DescribeIgnoredCouleur(rule, Reads(SheetRuleMember.CouleurEtiquette), loc));
        return ignored;
    }

    // Mirrors CouleurEtiquetteResolver: with a cell the default is never used; without one the allowed
    // list has nothing to filter.
    private static IEnumerable<string> DescribeIgnoredCouleur(
        SheetExtractionRule rule, bool isRead, IStringLocalizer<BlazorAdminMessages> loc)
    {
        if (!isRead)
        {
            if (rule.CouleurEtiquetteCell is { } cell)
            {
                yield return loc["ImportProfileDetails_IgnoredCouleurCell", CellRange(rule.Locator.FirstBlockStartRow, cell)];
            }

            if (rule.DefaultCouleurEtiquette is not null)
            {
                yield return loc["ImportProfileDetails_IgnoredDefaultCouleur", Quote(rule.DefaultCouleurEtiquette, loc)];
            }

            if (rule.AllowedCouleursEtiquette is not null)
            {
                yield return loc["ImportProfileDetails_IgnoredAllowedCouleurs", QuoteList(rule.AllowedCouleursEtiquette, loc)];
            }

            yield break;
        }

        if (rule.CouleurEtiquetteCell is not null && rule.DefaultCouleurEtiquette is not null)
        {
            yield return loc["ImportProfileDetails_IgnoredDefaultCouleurBecauseCell", Quote(rule.DefaultCouleurEtiquette, loc)];
        }

        if (rule.CouleurEtiquetteCell is null && rule.AllowedCouleursEtiquette is not null)
        {
            yield return loc["ImportProfileDetails_IgnoredAllowedCouleursWithoutCell", QuoteList(rule.AllowedCouleursEtiquette, loc)];
        }
    }

    // Names the extraction service looks up by name: a missing one makes it fail.
    private static List<string> DescribeBlocking(
        SheetExtractionRule rule, ImportSheetUsageEntry usage, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var blocking = new List<string>();
        if (usage.ReadMembers.Contains(SheetRuleMember.HeaderRules))
        {
            blocking.AddRange(usage.RequiredHeaderFields
                .Where(required => rule.HeaderFields.All(f => f.Name != required.Name))
                .Select(required => loc["ImportProfileDetails_BlockingMissingHeaderField", Quote(required.Name, loc)].Value));
            blocking.AddRange(usage.RequiredHeaderComposites
                .Where(required => rule.HeaderComposites.All(c => c.Name != required.Name))
                .Select(required => loc["ImportProfileDetails_BlockingMissingHeaderComposite", Quote(required.Name, loc)].Value));
        }

        blocking.AddRange(usage.RequiredBlockFieldNames
            .Where(name => rule.Locator.Fields.All(f => f.Name != name))
            .Select(name => loc["ImportProfileDetails_BlockingMissingBlockField", Quote(name, loc)].Value));

        if (usage.FixedStopFieldName is null && rule.Locator.Fields.All(f => f.Name != rule.Locator.StopFieldName))
        {
            blocking.Add(loc["ImportProfileDetails_BlockingStopFieldNotInBlock", Quote(rule.Locator.StopFieldName, loc)]);
        }

        return blocking;
    }

    // Only what extraction uses: the header names the sheet requires, plus any field a used composite
    // references. Anything else is stored but unused (reported separately, D2).
    private static IEnumerable<ProfileDescriptionSentence> DescribeHeader(
        SheetExtractionRule rule, ImportSheetUsageEntry usage, string reperePrefix, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var (usedComposites, referencedFieldNames) = UsedHeaderRules(rule, usage);

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
            yield return new(loc[key, Quote(field.Name, loc), CellRef(field.Cell.Range), options]);
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

    // Mirrors CouleurEtiquetteResolver: a configured cell wins, and the default is then never used (even for
    // a blank cell); the allowed list only filters a cell's value.
    private static IEnumerable<ProfileDescriptionSentence> DescribeCouleur(SheetExtractionRule rule, IStringLocalizer<BlazorAdminMessages> loc)
    {
        if (rule.CouleurEtiquetteCell is { } cell)
        {
            var range = CellRange(rule.Locator.FirstBlockStartRow, cell);
            yield return new(rule.AllowedCouleursEtiquette is null
                ? loc["ImportProfileDetails_CouleurFromCellAnyValue", range]
                : loc["ImportProfileDetails_CouleurFromCellWithAllowed", range, QuoteList(rule.AllowedCouleursEtiquette, loc)]);
        }
        else if (rule.DefaultCouleurEtiquette is not null)
        {
            yield return new(loc["ImportProfileDetails_CouleurDefault", Quote(rule.DefaultCouleurEtiquette, loc)]);
        }
    }

    // Colonnes ticked for every element, then conditional rules grouped the reverse way from the engine
    // (by field, operator and compared value rather than by Colonne), then cell-driven rules.
    private static IEnumerable<ProfileDescriptionSentence> DescribePoints(
        SheetExtractionRule rule, ImportSheetUsageEntry usage, IStringLocalizer<BlazorAdminMessages> loc)
    {
        if (usage.ReadMembers.Contains(SheetRuleMember.UnconditionalColonnes) && rule.UnconditionalColonneNames.Count > 0)
        {
            yield return new(OneOrSeveral(rule.UnconditionalColonneNames, loc,
                "ImportProfileDetails_PointUnconditionalOne", "ImportProfileDetails_PointUnconditionalSeveral"));
        }

        if (usage.ReadMembers.Contains(SheetRuleMember.ConditionalPointRules) && rule.PointRules.Count > 0)
        {
            foreach (var group in GroupConditionalRules(rule.PointRules))
            {
                yield return new(DescribeConditionalGroup(rule, group, loc));
            }

            yield return new(loc["ImportProfileDetails_PointNoConditionMet"]);
        }

        if (usage.ReadMembers.Contains(SheetRuleMember.FieldPresencePointRules))
        {
            var groups = rule.FieldPresencePointRules.GroupBy(
                r => (r.ColonneName, ExpectedValue: r.ExpectedValue?.Trim().ToUpperInvariant()));
            foreach (var group in groups)
            {
                var cells = JoinWithOr(
                    [.. group.Select(r => CellRange(rule.Locator.FirstBlockStartRow, r.Cell))
                        .Distinct()],
                    loc);
                var colonne = Quote(group.Key.ColonneName, loc);
                var expectedValue = group.First().ExpectedValue;
                yield return new(expectedValue is null
                    ? loc["ImportProfileDetails_PointCellFilledIn", cells, colonne]
                    : loc["ImportProfileDetails_PointCellHasValue", cells, Quote(expectedValue.Trim(), loc), colonne]);
            }
        }
    }

    // Same normalization as ConditionalPointRuleEvaluator: compared value trimmed, case ignored.
    private static IEnumerable<IGrouping<(string SourceFieldName, ConditionOperator Operator, string Value), ConditionalPointRule>>
        GroupConditionalRules(IEnumerable<ConditionalPointRule> rules) =>
        rules.GroupBy(r => (r.SourceFieldName, r.Operator, Value: r.ComparisonValue.Trim().ToUpperInvariant()));

    private static string DescribeConditionalGroup(
        SheetExtractionRule rule,
        IGrouping<(string SourceFieldName, ConditionOperator Operator, string Value), ConditionalPointRule> group,
        IStringLocalizer<BlazorAdminMessages> loc)
    {
        var colonnes = group.Select(r => r.ColonneName).Distinct(StringComparer.Ordinal).ToList();
        var first = group.First();

        // ISOLEMENT's zero-energie rule compares a computed "true"/"false" flag -- meaningless read
        // literally, so it's described through the cell and ZeroEnergieExpectedValue that produce it.
        if (first.SourceFieldName == IsolementFieldNames.HasZeroEnergie
            && first.Operator == ConditionOperator.Equals
            && group.Key.Value == bool.TrueString.ToUpperInvariant())
        {
            var isEvaluated = rule.ZeroEnergieExpectedValue is not null
                && rule.Locator.Fields.Any(f => f.Name == IsolementFieldNames.HasZeroEnergie);
            return isEvaluated
                ? colonnes.Count == 1
                    ? loc["ImportProfileDetails_PointZeroEnergieOne", Quote(rule.ZeroEnergieExpectedValue!, loc), Quote(colonnes[0], loc)]
                    : loc["ImportProfileDetails_PointZeroEnergieSeveral", Quote(rule.ZeroEnergieExpectedValue!, loc), colonnes.Count,
                        QuoteList(colonnes, loc)]
                : OneOrSeveral(colonnes, loc,
                    "ImportProfileDetails_PointZeroEnergieNeverOne", "ImportProfileDetails_PointZeroEnergieNeverSeveral");
        }

        var field = FieldLabel(first.SourceFieldName, definite: true, loc);
        var value = Quote(first.ComparisonValue.Trim(), loc);
        var isEquals = first.Operator == ConditionOperator.Equals;
        return colonnes.Count == 1
            ? loc[isEquals ? "ImportProfileDetails_PointEqualsOne" : "ImportProfileDetails_PointNotEqualsOne", field, value, Quote(colonnes[0], loc)]
            : loc[isEquals ? "ImportProfileDetails_PointEqualsSeveral" : "ImportProfileDetails_PointNotEqualsSeveral", field, value,
                colonnes.Count, QuoteList(colonnes, loc)];
    }

    // D1: where the data is read -- step, start row, stop field, then each field's range in the first block.
    private static IEnumerable<ProfileDescriptionSentence> DescribeBlocks(
        RepeatingBlockLocator locator, ImportSheetUsageEntry usage, IStringLocalizer<BlazorAdminMessages> loc)
    {
        // The field reading really stops on -- not the configured one when the service ignores it.
        var stopField = FieldLabel(usage.FixedStopFieldName ?? locator.StopFieldName, definite: true, loc);
        var isTask = usage.ItemKind == BlockItemKind.Task;

        yield return new(locator.Step == 1
            ? loc[isTask ? "ImportProfileDetails_BlockTaskPerLine" : "ImportProfileDetails_BlockElementPerLine",
                locator.FirstBlockStartRow, stopField]
            : loc[isTask ? "ImportProfileDetails_BlockTaskEveryNLines" : "ImportProfileDetails_BlockElementEveryNLines",
                locator.Step, locator.FirstBlockStartRow, stopField]);

        var fields = string.Join(ListSeparator, locator.Fields.Select(field => loc[
            "ImportProfileDetails_BlockFieldAt",
            FieldLabel(field.Name, definite: false, loc),
            CellRange(locator.FirstBlockStartRow, field)].Value));

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

        // ImportPipelineOrchestrator.FindRule requires all 6 sheets: a missing one makes the import fail.
        var blocking = ImportSheetUsage.KnownSheetNames
            .Where(sheetName => profile.SheetRules.All(r => r.SheetName != sheetName))
            .Select(sheetName => loc["ImportProfileDetails_BlockingMissingSheet", Quote(sheetName, loc)].Value)
            .ToList();

        return new ProfileDescriptionSection(loc["ImportProfileDetails_GeneralSectionTitle"], sentences, [], Marked(blocking));
    }

    private static string CountedSentence(
        IReadOnlyList<string> values, IStringLocalizer<BlazorAdminMessages> loc, string noneKey, string oneKey, string severalKey) =>
        values.Count == 0 ? loc[noneKey] : OneOrSeveral(values, loc, oneKey, severalKey);

    // Absolute range of a block cell in the first block.
    private static string CellRange(int firstBlockStartRow, BlockFieldDefinition cell) =>
        CellRef(BlockFieldRangeFormatter.ToAbsoluteRange(firstBlockStartRow, cell.ColumnRange, cell.RowOffsetStart, cell.RowOffsetEnd));
}
