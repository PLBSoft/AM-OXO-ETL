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

        return new ProfileDescriptionSection(loc["ExportProfileDetails_WorkbookSectionTitle"], sentences, [], []);
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

        sentences.AddRange(DescribeColumns(columns, loc));
        return new ProfileDescriptionSection(title, sentences, [], []);
    }

    private static IEnumerable<ProfileDescriptionSentence> DescribeColumns(
        IReadOnlyList<ExportColumn> columns, IStringLocalizer<BlazorAdminMessages> loc)
    {
        foreach (var column in columns)
        {
            switch (column.Definition)
            {
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

    // "Colonne A « Repère » : {content}." -- the letter marked as a cell coordinate (D1).
    private static ProfileDescriptionSentence ColumnSentence(
        ExportColumn column, string content, IStringLocalizer<BlazorAdminMessages> loc, bool isFixed = false) =>
        new(loc["ExportProfileDetails_Column", CellRef(column.Letter), Quote(column.Header, loc), content], isFixed);
}
