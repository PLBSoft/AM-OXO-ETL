using ExcelETL.BlazorAdmin.Resources;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using Microsoft.Extensions.Localization;
using static ExcelETL.BlazorAdmin.Formatting.ProfileDescriptionMarking;

namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 079 (docs/tickets/tickets-tdd-lot-079-vue-details-langage-courant-profil-export.md): turns an
// ExportProfile into plain-language sentences describing the generated workbook, sheet by sheet and column
// by column. Presentation only: no rule is rebuilt or validated here. Sections and columns follow the
// profile's own order, which is the order SheetGenerationEngine and ClosedXmlWorkbookWriter write.
// ExportProfileDetails_* keys, French text in both .resx files for now (decision D7).
public static class ExportProfileDescriptionBuilder
{
    // Codes produced by ProcedureExtractionService's fixed MAD/REL conversion -- the only type codes known in
    // advance; any other source value is kept as is.
    private static readonly string[] KnownTacheMultipleCodes = ["TM_PROC_MAD", "TM_PROC_REL"];

    public static ProfileDescription Build(ExportProfile profile, IStringLocalizer<BlazorAdminMessages> loc)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(loc);

        var sections = new List<ProfileDescriptionSection> { BuildWorkbookSection(profile, loc) };
        sections.AddRange(profile.SheetRules.Select(rule => BuildSheetSection(rule, loc)));
        return new ProfileDescription(sections);
    }

    private static ProfileDescriptionSection BuildWorkbookSection(ExportProfile profile, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var items = profile.SheetRules.Select(rule => rule.PivotSource == PivotSource.TacheMultiple
                ? loc["ExportProfileDetails_WorkbookItemTacheMultiple", Quote(rule.SheetName, loc)].Value
                : loc["ExportProfileDetails_WorkbookItemSheet", Quote(rule.SheetName, loc)].Value)
            .ToList();

        var sentences = new List<ProfileDescriptionSentence>
        {
            new(items.Count == 1
                ? loc["ExportProfileDetails_WorkbookSheetsOne", items[0]]
                : loc["ExportProfileDetails_WorkbookSheetsSeveral", JoinWithAnd(items, loc)]),
            new(loc["ExportProfileDetails_WorkbookHeaderRow"], IsFixed: true),
            new(loc["ExportProfileDetails_WorkbookImportProfileDependency"]),
        };

        return new ProfileDescriptionSection(
            loc["ExportProfileDetails_WorkbookSectionTitle"], sentences, [], Marked(DescribeWorkbookBlocking(profile, loc)));
    }

    // D3: sheet-name clashes between rules that make ClosedXmlWorkbookWriter fail.
    private static List<string> DescribeWorkbookBlocking(ExportProfile profile, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var blocking = new List<string>();
        var namedSheets = profile.SheetRules.Where(r => r.PivotSource != PivotSource.TacheMultiple).Select(r => r.SheetName).ToList();
        var tacheMultipleRules = profile.SheetRules.Where(r => r.PivotSource == PivotSource.TacheMultiple).ToList();

        blocking.AddRange(namedSheets
            .GroupBy(name => name, ExcelSheetNameRules.NameComparer)
            .Where(group => group.Count() > 1)
            .Select(group => loc["ExportProfileDetails_BlockingDuplicateSheetName", JoinWithAnd([.. group.Select(name => Quote(name, loc))], loc)].Value));

        if (tacheMultipleRules.Count > 1)
        {
            blocking.Add(loc["ExportProfileDetails_BlockingSeveralTacheMultipleRules", tacheMultipleRules.Count,
                QuoteList(tacheMultipleRules.Select(r => r.SheetName), loc)]);
        }

        if (tacheMultipleRules.Count > 0)
        {
            blocking.AddRange(namedSheets
                .Where(name => KnownTacheMultipleCodes.Contains(name, ExcelSheetNameRules.NameComparer))
                .Select(name => loc["ExportProfileDetails_BlockingSheetNamedLikeTacheMultipleCode", Quote(name, loc)].Value));
        }

        return blocking;
    }

    // D3: a sheet name ClosedXML refuses. A TacheMultiple rule's name never becomes a sheet name.
    private static List<string> DescribeSheetNameBlocking(SheetGenerationRule rule, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var blocking = new List<string>();
        if (rule.PivotSource == PivotSource.TacheMultiple)
        {
            return blocking;
        }

        var name = Quote(rule.SheetName, loc);
        if (ExcelSheetNameRules.IsTooLong(rule.SheetName))
        {
            blocking.Add(loc["ExportProfileDetails_BlockingNameTooLong", name, ExcelSheetNameRules.MaxLength, rule.SheetName.Length]);
        }

        var forbidden = ExcelSheetNameRules.ForbiddenCharactersIn(rule.SheetName);
        if (forbidden.Count > 0)
        {
            blocking.Add(loc["ExportProfileDetails_BlockingNameForbiddenCharacter", name, QuoteList(forbidden.Select(c => c.ToString()), loc)]);
        }

        if (ExcelSheetNameRules.HasApostropheAtEdge(rule.SheetName))
        {
            blocking.Add(loc["ExportProfileDetails_BlockingNameApostrophe", name]);
        }

        return blocking;
    }

    private static ProfileDescriptionSection BuildSheetSection(SheetGenerationRule rule, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var sentences = new List<ProfileDescriptionSentence>();
        string title;
        switch (rule.PivotSource)
        {
            case PivotSource.Equipement:
                title = loc["ExportProfileDetails_SheetSectionTitle", rule.SheetName];
                sentences.Add(new(loc["ExportProfileDetails_SheetRowsEquipement"], IsFixed: true));
                break;
            case PivotSource.Isolement:
                title = loc["ExportProfileDetails_SheetSectionTitle", rule.SheetName];
                sentences.Add(new(loc["ExportProfileDetails_SheetRowsIsolement"], IsFixed: true));
                break;
            default:
                title = loc["ExportProfileDetails_TacheMultipleSectionTitle", rule.SheetName];
                sentences.Add(new(loc["ExportProfileDetails_SheetTacheMultipleSheets",
                    Quote(rule.SheetName, loc), Quote(KnownTacheMultipleCodes[0], loc), Quote(KnownTacheMultipleCodes[1], loc)], IsFixed: true));
                sentences.Add(new(loc["ExportProfileDetails_SheetTacheMultipleRows"], IsFixed: true));
                break;
        }

        var columns = ExportColumnLayout.For(rule);
        if (columns.Count == 0)
        {
            sentences.Add(new(loc["ExportProfileDetails_SheetNoColumn"]));
        }

        sentences.AddRange(DescribeColumns(columns, rule.PivotSource == PivotSource.Equipement, loc));
        return new ProfileDescriptionSection(title, sentences, [], Marked(DescribeSheetNameBlocking(rule, loc)));
    }

    private static IEnumerable<ProfileDescriptionSentence> DescribeColumns(
        IReadOnlyList<ExportColumn> columns, bool isEquipement, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var pointGroups = GroupPointColumns(columns);

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            switch (column.Definition)
            {
                case PointColumnDefinition point when pointGroups.TryGetValue(index, out var group):
                    if (group[0] == index)
                    {
                        yield return DescribePointGroup(columns, group, point.MarkValue, isEquipement, loc);
                    }

                    break;
                case PointColumnDefinition point:
                    yield return ColumnSentence(column, loc[isEquipement ? "ExportProfileDetails_PointEquipement" : "ExportProfileDetails_PointIsolement",
                        Quote(point.MarkValue, loc), Quote(point.ColonneNom, loc)], loc);
                    break;
                case ApplicationColumnDefinition application:
                    yield return ColumnSentence(column, loc[isEquipement ? "ExportProfileDetails_ApplicationEquipement" : "ExportProfileDetails_ApplicationIsolement",
                        Quote(application.MarkValue, loc), Quote(application.ApplicationNom, loc)], loc);
                    break;
                case ColumnDefinition { Source: null }:
                    yield return ColumnSentence(column, loc["ExportProfileDetails_ColumnEmpty"], loc);
                    break;
                case ColumnDefinition { Source: { } source }:
                    // CRITERE's two values are coded in PivotFieldResolver, not editable in the profile.
                    yield return source == PivotFieldRef.TacheMultipleCritere
                        ? ColumnSentence(column, loc["ExportProfileDetails_Field_TacheMultipleCritere", Quote("A faire", loc), Quote("Pour info", loc)], loc, isFixed: true)
                        : ColumnSentence(column, loc[$"ExportProfileDetails_Field_{source}"], loc);
                    break;
                case ConstantColumnDefinition constant:
                    yield return ColumnSentence(column, loc["ExportProfileDetails_ColumnConstant", Quote(constant.Value, loc)], loc);
                    break;
            }
        }
    }

    // D2: point columns whose header is their Colonne name, grouped by mark value; only groups of two or more
    // columns are described together. Key: column index; value: the indexes of its group, in order.
    private static Dictionary<int, List<int>> GroupPointColumns(IReadOnlyList<ExportColumn> columns)
    {
        var groups = columns
            .Select((column, index) => (Point: column.Definition as PointColumnDefinition, Index: index))
            .Where(c => c.Point is not null && c.Point.Header == c.Point.ColonneNom)
            .GroupBy(c => c.Point!.MarkValue, StringComparer.Ordinal)
            .Select(g => g.Select(c => c.Index).ToList())
            .Where(indexes => indexes.Count >= 2);

        return groups.SelectMany(indexes => indexes.Select(index => (index, indexes))).ToDictionary(p => p.index, p => p.indexes);
    }

    private static ProfileDescriptionSentence DescribePointGroup(
        IReadOnlyList<ExportColumn> columns, List<int> group, string markValue, bool isEquipement, IStringLocalizer<BlazorAdminMessages> loc)
    {
        var isConsecutive = group[^1] - group[0] == group.Count - 1;
        var letters = isConsecutive
            ? loc["ExportProfileDetails_PointGroupRange", CellRef(columns[group[0]].Letter), CellRef(columns[group[^1]].Letter)].Value
            : loc["ExportProfileDetails_PointGroupLetters", JoinWithAnd([.. group.Select(i => CellRef(columns[i].Letter))], loc)].Value;
        var items = string.Join(ListSeparator, group.Select(i =>
            loc["ExportProfileDetails_PointGroupItem", Quote(columns[i].Header, loc), CellRef(columns[i].Letter)].Value));

        return new(loc[isEquipement ? "ExportProfileDetails_PointGroupEquipement" : "ExportProfileDetails_PointGroupIsolement",
            letters, Quote(markValue, loc), items]);
    }

    // "Colonne A « Repère » : {content}." -- the letter marked as a cell coordinate (D1).
    private static ProfileDescriptionSentence ColumnSentence(
        ExportColumn column, string content, IStringLocalizer<BlazorAdminMessages> loc, bool isFixed = false) =>
        new(loc["ExportProfileDetails_Column", CellRef(column.Letter), Quote(column.Header, loc), content], isFixed);
}
