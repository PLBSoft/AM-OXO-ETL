using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.BlazorAdmin.Editing.Import;

// The conversions at the center of the import editor's draft architecture (lot 075,
// docs/tickets/tickets-tdd-lot-075-migration-editeur-import-brouillon.md), same contract as
// ExportProfileDraftMapper (lot 074): FromDomain builds a draft tree from a persisted ImportProfile;
// ToDomain builds an ImportProfile back, or reports every failure found anywhere in the tree. Business
// rules stay in the Domain: every ConvertXxx calls a real Domain constructor or validator and forwards
// what it throws. The only rules checked here are those no Domain type owns (an Excel range that must
// parse, an unconditional Colonne name that must not be blank) -- reported as DraftValidationException.
//
// Conversions never localize anything, but they do mutate the drafts they are given:
// - a pending "Add a ..." row still exactly as newly built (DraftJson.IsPristine) is ignored; a
//   non-pristine valid one is promoted at the end of its list and its slot replaced by a fresh
//   instance; a non-pristine invalid one fails the conversion (defect A suppressed by construction);
// - a successful conversion writes back the normalized form of what was typed (trimmed Tableau and
//   Application names, canonical Excel ranges), so the summaries show what the Domain stores.
public static class ImportProfileDraftMapper
{
    // Stored names of cells no input exposes, used for newly created ones (edited ones keep theirs).
    public const string DefaultFieldPresenceCellName = "FieldPresenceCell";
    public const string DefaultCouleurEtiquetteCellName = "CouleurEtiquette";

    // Never read: a header field's real sheet is always its rule's sheet, applied when the rule is
    // built. Lets a header field be validated on its own before its rule has a sheet name.
    private const string PendingHeaderFieldSheet = "__pending__";

    public static ImportProfileDraft FromDomain(ImportProfile profile) => new()
    {
        Id = profile.Id,
        Name = profile.Name,
        ReperePrefix = profile.ReperePrefix,
        EquipementTypeElementNom = profile.EquipementTypeElementNom,
        DefaultTableaux = [.. profile.DefaultTableaux.Select(v => new StringItemDraft { Value = v })],
        DefaultApplicationNames = [.. profile.DefaultApplicationNames.Select(v => new StringItemDraft { Value = v })],
        TacheMultipleTypeLabels =
            [.. profile.TacheMultipleTypeLabels.Select(l => new TacheMultipleTypeLabelDraft { Code = l.Code, Label = l.Label })],
        SheetRules = [.. profile.SheetRules.Select(FromDomainRule)],
    };

    private static SheetExtractionRuleDraft FromDomainRule(SheetExtractionRule rule)
    {
        var startRow = rule.Locator.FirstBlockStartRow;
        return new SheetExtractionRuleDraft
        {
            SheetName = rule.SheetName,
            FirstBlockStartRow = startRow,
            Step = rule.Locator.Step,
            StopFieldName = rule.Locator.StopFieldName,
            ZeroEnergieExpectedValue = rule.ZeroEnergieExpectedValue ?? string.Empty,
            DefaultCouleurEtiquette = rule.DefaultCouleurEtiquette ?? string.Empty,
            CouleurEtiquetteCellRange = rule.CouleurEtiquetteCell is { } cell ? ToAbsoluteRange(startRow, cell) : string.Empty,
            CouleurEtiquetteCellName = rule.CouleurEtiquetteCell?.Name,
            AllowedCouleursEtiquette = rule.AllowedCouleursEtiquette is null ? string.Empty : string.Join(", ", rule.AllowedCouleursEtiquette),
            Fields = [.. rule.Locator.Fields.Select(f => new BlockFieldDefinitionDraft { Name = f.Name, AbsoluteRange = ToAbsoluteRange(startRow, f) })],
            UnconditionalColonneNames = [.. rule.UnconditionalColonneNames.Select(v => new StringItemDraft { Value = v })],
            PointRules =
            [
                .. rule.PointRules.Select(p => new ConditionalPointRuleDraft
                {
                    ColonneName = p.ColonneName, SourceFieldName = p.SourceFieldName, Operator = p.Operator, ComparisonValue = p.ComparisonValue,
                }),
            ],
            FieldPresencePointRules =
            [
                .. rule.FieldPresencePointRules.Select(f => new FieldPresencePointRuleDraft
                {
                    ColonneName = f.ColonneName,
                    AbsoluteRange = ToAbsoluteRange(startRow, f.Cell),
                    ExpectedValue = f.ExpectedValue ?? string.Empty,
                    CellName = f.Cell.Name,
                }),
            ],
            HeaderFields =
            [
                .. rule.HeaderFields.Select(h => new HeaderFieldRuleDraft
                {
                    Name = h.Name, Range = h.Cell.Range, DateFormat = h.DateFormat ?? string.Empty, StripReperePrefix = h.StripReperePrefix,
                }),
            ],
            HeaderComposites = [.. rule.HeaderComposites.Select(c => new HeaderCompositeRuleDraft { Name = c.Name, Template = c.Template })],
        };
    }

    // ------------------------------------------------------------------------------------------
    // Whole profile.
    // ------------------------------------------------------------------------------------------

    public static ConversionResult<ImportProfile> ToDomain(ImportProfileDraft draft)
    {
        var errors = new List<DraftConversionError>();

        var tableaux = ConvertUniqueList(draft.DefaultTableaux, draft.PendingDefaultTableau, ConvertDefaultTableau, errors,
            () => { draft.DefaultTableaux.Add(draft.PendingDefaultTableau); draft.PendingDefaultTableau = new StringItemDraft(); });
        var applicationNames = ConvertUniqueList(draft.DefaultApplicationNames, draft.PendingDefaultApplicationName, ConvertDefaultApplicationName, errors,
            () => { draft.DefaultApplicationNames.Add(draft.PendingDefaultApplicationName); draft.PendingDefaultApplicationName = new StringItemDraft(); });
        var labels = ConvertUniqueList(draft.TacheMultipleTypeLabels, draft.PendingTacheMultipleTypeLabel, ConvertTacheMultipleTypeLabel, errors,
            () => { draft.TacheMultipleTypeLabels.Add(draft.PendingTacheMultipleTypeLabel); draft.PendingTacheMultipleTypeLabel = new TacheMultipleTypeLabelDraft(); });

        var rules = ConvertList(draft.SheetRules, ConvertSheetRule, errors);
        PromoteIfNonPristineAndValid(draft.PendingSheetRule, ConvertSheetRule, rules, errors,
            () => { draft.SheetRules.Add(draft.PendingSheetRule); draft.PendingSheetRule = new SheetExtractionRuleDraft(); });

        if (errors.Count > 0)
        {
            return ConversionResult<ImportProfile>.Failure(errors);
        }

        try
        {
            var profile = draft.Id is { } id
                ? new ImportProfile(id, draft.Name, draft.ReperePrefix, draft.EquipementTypeElementNom, tableaux, applicationNames, rules, labels)
                : new ImportProfile(draft.Name, draft.ReperePrefix, draft.EquipementTypeElementNom, tableaux, applicationNames, rules, labels);
            return ConversionResult<ImportProfile>.Success(profile);
        }
        catch (DomainValidationException ex)
        {
            return ConversionResult<ImportProfile>.Failure(draft, ex);
        }
    }

    // Root-list items are validated against the other items of their own list (duplicates), so their
    // unit conversions take those "others" -- in ToDomain, the items before them, as the ImportProfile
    // constructor does; from a row's own Save button, every other item of the list.
    public static ConversionResult<string> ConvertDefaultTableau(StringItemDraft draft, IEnumerable<StringItemDraft> others) =>
        ConvertListItemName(draft, others, ImportProfile.ValidateDefaultTableauName);

    public static ConversionResult<string> ConvertDefaultApplicationName(StringItemDraft draft, IEnumerable<StringItemDraft> others) =>
        ConvertListItemName(draft, others, ImportProfile.ValidateDefaultApplicationName);

    public static ConversionResult<TacheMultipleTypeLabel> ConvertTacheMultipleTypeLabel(
        TacheMultipleTypeLabelDraft draft, IEnumerable<TacheMultipleTypeLabelDraft> others)
    {
        try
        {
            var label = new TacheMultipleTypeLabel(draft.Code, draft.Label);
            // Only the others' codes matter here; an invalid other (reported on its own draft) is skipped.
            var existing = others
                .Where(o => !string.IsNullOrWhiteSpace(o.Code) && o.Code.Trim().Length <= ImportProfile.MaxListItemNameLength)
                .Select(o => new TacheMultipleTypeLabel(o.Code, "-"))
                .ToList();
            ImportProfile.ValidateTacheMultipleTypeLabelCode(label, existing);
            return ConversionResult<TacheMultipleTypeLabel>.Success(label);
        }
        catch (DomainValidationException ex)
        {
            return ConversionResult<TacheMultipleTypeLabel>.Failure(draft, ex);
        }
    }

    private static ConversionResult<string> ConvertListItemName(
        StringItemDraft draft, IEnumerable<StringItemDraft> others, Action<string, IReadOnlyList<string>> validate)
    {
        try
        {
            validate(draft.Value, others.Select(o => o.Value.Trim()).ToList());
            draft.Value = draft.Value.Trim();
            return ConversionResult<string>.Success(draft.Value);
        }
        catch (DomainValidationException ex)
        {
            return ConversionResult<string>.Failure(draft, ex);
        }
    }

    // ------------------------------------------------------------------------------------------
    // One sheet rule -- reused by the rule form's own submit button, by the lot 057 form switch, and
    // by ToDomain's per-rule loop: one validation path.
    // ------------------------------------------------------------------------------------------

    public static ConversionResult<SheetExtractionRule> ConvertSheetRule(SheetExtractionRuleDraft draft)
    {
        var errors = new List<DraftConversionError>();
        var startRow = draft.FirstBlockStartRow;

        ConversionResult<BlockFieldDefinition> Field(BlockFieldDefinitionDraft d) => ConvertField(d, startRow);
        ConversionResult<FieldPresencePointRule> FieldPresence(FieldPresencePointRuleDraft d) => ConvertFieldPresencePointRule(d, startRow);

        var fields = ConvertList(draft.Fields, Field, errors);
        PromoteIfNonPristineAndValid(draft.PendingField, Field, fields, errors,
            () => { draft.Fields.Add(draft.PendingField); draft.PendingField = new BlockFieldDefinitionDraft(); });

        var unconditionalColonneNames = ConvertList(draft.UnconditionalColonneNames, ConvertUnconditionalColonneName, errors);
        // A blank pending Colonne name is ignored, never an error -- same as clicking its Add button.
        if (!string.IsNullOrWhiteSpace(draft.PendingUnconditionalColonneName.Value))
        {
            PromoteIfNonPristineAndValid(draft.PendingUnconditionalColonneName, ConvertUnconditionalColonneName, unconditionalColonneNames, errors,
                () => { draft.UnconditionalColonneNames.Add(draft.PendingUnconditionalColonneName); draft.PendingUnconditionalColonneName = new StringItemDraft(); });
        }

        var pointRules = ConvertList(draft.PointRules, ConvertPointRule, errors);
        PromoteIfNonPristineAndValid(draft.PendingPointRule, ConvertPointRule, pointRules, errors,
            () => { draft.PointRules.Add(draft.PendingPointRule); draft.PendingPointRule = new ConditionalPointRuleDraft(); });

        var fieldPresencePointRules = ConvertList(draft.FieldPresencePointRules, FieldPresence, errors);
        PromoteIfNonPristineAndValid(draft.PendingFieldPresencePointRule, FieldPresence, fieldPresencePointRules, errors,
            () => { draft.FieldPresencePointRules.Add(draft.PendingFieldPresencePointRule); draft.PendingFieldPresencePointRule = new FieldPresencePointRuleDraft(); });

        var headerFields = ConvertList(draft.HeaderFields, ConvertHeaderField, errors);
        PromoteIfNonPristineAndValid(draft.PendingHeaderField, ConvertHeaderField, headerFields, errors,
            () => { draft.HeaderFields.Add(draft.PendingHeaderField); draft.PendingHeaderField = new HeaderFieldRuleDraft(); });

        var headerComposites = ConvertList(draft.HeaderComposites, ConvertHeaderComposite, errors);
        PromoteIfNonPristineAndValid(draft.PendingHeaderComposite, ConvertHeaderComposite, headerComposites, errors,
            () => { draft.HeaderComposites.Add(draft.PendingHeaderComposite); draft.PendingHeaderComposite = new HeaderCompositeRuleDraft(); });

        if (errors.Count > 0)
        {
            return ConversionResult<SheetExtractionRule>.Failure(errors);
        }

        // Blank -> no per-block cell configured (null, not an error); a non-blank value must parse.
        BlockFieldDefinition? couleurEtiquetteCell = null;
        if (!string.IsNullOrWhiteSpace(draft.CouleurEtiquetteCellRange))
        {
            var parsed = BlockFieldRangeFormatter.FromAbsoluteRange(draft.CouleurEtiquetteCellRange, startRow);
            if (!parsed.IsSuccess)
            {
                return ConversionResult<SheetExtractionRule>.Failure(draft, InvalidExcelRange());
            }

            couleurEtiquetteCell = new BlockFieldDefinition(
                draft.CouleurEtiquetteCellName ?? DefaultCouleurEtiquetteCellName,
                parsed.ColumnRange, parsed.RowOffsetStart, parsed.RowOffsetEnd);
            draft.CouleurEtiquetteCellRange = ToAbsoluteRange(startRow, couleurEtiquetteCell);
        }

        try
        {
            var locator = new RepeatingBlockLocator(draft.SheetName, startRow, draft.Step, draft.StopFieldName, fields);

            var rule = new SheetExtractionRule(
                draft.SheetName, locator, pointRules, unconditionalColonneNames,
                [.. headerFields.Select(h => new HeaderFieldRule(h.Name, new DirectCell(draft.SheetName, h.Cell.Range), h.StripReperePrefix, h.DateFormat))],
                headerComposites,
                NullIfBlank(draft.ZeroEnergieExpectedValue),
                fieldPresencePointRules: fieldPresencePointRules,
                couleurEtiquetteCell: couleurEtiquetteCell,
                defaultCouleurEtiquette: NullIfBlank(draft.DefaultCouleurEtiquette),
                allowedCouleursEtiquette: ParseAllowedCouleursEtiquette(draft.AllowedCouleursEtiquette));
            return ConversionResult<SheetExtractionRule>.Success(rule);
        }
        catch (Exception ex) when (ex is DomainValidationException or DomainArgumentOutOfRangeException or DomainRuleViolationException)
        {
            return ConversionResult<SheetExtractionRule>.Failure(draft, ex);
        }
    }

    public static ConversionResult<BlockFieldDefinition> ConvertField(BlockFieldDefinitionDraft draft, int firstBlockStartRow)
    {
        var parsed = BlockFieldRangeFormatter.FromAbsoluteRange(draft.AbsoluteRange, firstBlockStartRow);
        if (!parsed.IsSuccess)
        {
            return ConversionResult<BlockFieldDefinition>.Failure(draft, InvalidExcelRange());
        }

        try
        {
            var field = new BlockFieldDefinition(draft.Name, parsed.ColumnRange, parsed.RowOffsetStart, parsed.RowOffsetEnd);
            draft.AbsoluteRange = ToAbsoluteRange(firstBlockStartRow, field);
            return ConversionResult<BlockFieldDefinition>.Success(field);
        }
        catch (DomainValidationException ex)
        {
            return ConversionResult<BlockFieldDefinition>.Failure(draft, ex);
        }
    }

    public static ConversionResult<FieldPresencePointRule> ConvertFieldPresencePointRule(FieldPresencePointRuleDraft draft, int firstBlockStartRow)
    {
        var parsed = BlockFieldRangeFormatter.FromAbsoluteRange(draft.AbsoluteRange, firstBlockStartRow);
        if (!parsed.IsSuccess)
        {
            return ConversionResult<FieldPresencePointRule>.Failure(draft, InvalidExcelRange());
        }

        try
        {
            var cell = new BlockFieldDefinition(
                draft.CellName ?? DefaultFieldPresenceCellName, parsed.ColumnRange, parsed.RowOffsetStart, parsed.RowOffsetEnd);
            var rule = new FieldPresencePointRule(cell, draft.ColonneName, NullIfBlank(draft.ExpectedValue)?.Trim());
            draft.AbsoluteRange = ToAbsoluteRange(firstBlockStartRow, cell);
            return ConversionResult<FieldPresencePointRule>.Success(rule);
        }
        catch (DomainValidationException ex)
        {
            return ConversionResult<FieldPresencePointRule>.Failure(draft, ex);
        }
    }

    public static ConversionResult<string> ConvertUnconditionalColonneName(StringItemDraft draft) =>
        string.IsNullOrWhiteSpace(draft.Value)
            ? ConversionResult<string>.Failure(draft, new DraftValidationException("ImportProfileEditor_EmptyColonneNameError"))
            : ConversionResult<string>.Success(draft.Value);

    public static ConversionResult<ConditionalPointRule> ConvertPointRule(ConditionalPointRuleDraft draft)
    {
        try
        {
            return ConversionResult<ConditionalPointRule>.Success(
                new ConditionalPointRule(draft.SourceFieldName, draft.Operator, draft.ComparisonValue, draft.ColonneName));
        }
        catch (DomainValidationException ex)
        {
            return ConversionResult<ConditionalPointRule>.Failure(draft, ex);
        }
    }

    public static ConversionResult<HeaderFieldRule> ConvertHeaderField(HeaderFieldRuleDraft draft)
    {
        try
        {
            return ConversionResult<HeaderFieldRule>.Success(new HeaderFieldRule(
                draft.Name, new DirectCell(PendingHeaderFieldSheet, draft.Range), draft.StripReperePrefix, NullIfBlank(draft.DateFormat)));
        }
        catch (DomainValidationException ex)
        {
            return ConversionResult<HeaderFieldRule>.Failure(draft, ex);
        }
    }

    public static ConversionResult<HeaderCompositeRule> ConvertHeaderComposite(HeaderCompositeRuleDraft draft)
    {
        try
        {
            return ConversionResult<HeaderCompositeRule>.Success(new HeaderCompositeRule(draft.Name, draft.Template));
        }
        catch (DomainValidationException ex)
        {
            return ConversionResult<HeaderCompositeRule>.Failure(draft, ex);
        }
    }

    // ------------------------------------------------------------------------------------------
    // Helpers.
    // ------------------------------------------------------------------------------------------

    private static DraftValidationException InvalidExcelRange() => new("ImportProfileEditor_InvalidExcelRangeError");

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static IReadOnlyList<string>? ParseAllowedCouleursEtiquette(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var entries = text.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        return entries.Count == 0 ? null : entries;
    }

    private static string ToAbsoluteRange(int firstBlockStartRow, BlockFieldDefinition cell) =>
        BlockFieldRangeFormatter.ToAbsoluteRange(firstBlockStartRow, cell.ColumnRange, cell.RowOffsetStart, cell.RowOffsetEnd);

    private static List<TDomain> ConvertList<TDraft, TDomain>(
        List<TDraft> drafts, Func<TDraft, ConversionResult<TDomain>> convert, List<DraftConversionError> errors)
    {
        var values = new List<TDomain>();
        foreach (var item in drafts)
        {
            var result = convert(item);
            if (result.IsSuccess)
            {
                values.Add(result.Value!);
            }
            else
            {
                errors.AddRange(result.Errors);
            }
        }

        return values;
    }

    // A list whose items must be unique: each item is checked against the items before it, the
    // pending row against the whole list.
    private static List<TDomain> ConvertUniqueList<TDraft, TDomain>(
        List<TDraft> drafts, TDraft pending, Func<TDraft, IEnumerable<TDraft>, ConversionResult<TDomain>> convert,
        List<DraftConversionError> errors, Action promote)
        where TDraft : notnull, new()
    {
        var values = new List<TDomain>();
        for (var i = 0; i < drafts.Count; i++)
        {
            var result = convert(drafts[i], drafts.Take(i));
            if (result.IsSuccess)
            {
                values.Add(result.Value!);
            }
            else
            {
                errors.AddRange(result.Errors);
            }
        }

        PromoteIfNonPristineAndValid(pending, p => convert(p, drafts), values, errors, promote);
        return values;
    }

    private static void PromoteIfNonPristineAndValid<TDraft, TDomain>(
        TDraft pending, Func<TDraft, ConversionResult<TDomain>> convert, List<TDomain> values,
        List<DraftConversionError> errors, Action promote)
        where TDraft : notnull, new()
    {
        if (DraftJson.IsPristine(pending))
        {
            return;
        }

        var result = convert(pending);
        if (result.IsSuccess)
        {
            values.Add(result.Value!);
            promote();
        }
        else
        {
            errors.AddRange(result.Errors);
        }
    }
}
