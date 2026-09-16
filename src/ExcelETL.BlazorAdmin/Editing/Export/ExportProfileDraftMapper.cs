using ExcelETL.BlazorAdmin.Editing;
using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;

namespace ExcelETL.BlazorAdmin.Editing.Export;

// The two pure conversions ("pure" in the sense of never touching a localizer or a store; each still
// mutates the draft tree it is given -- see the "Ligne en attente" bullet below) at the center of the
// P3 draft architecture (lot 074): FromDomain builds a fresh draft tree from an already-persisted
// ExportProfile, ToDomain builds a fresh ExportProfile back from a draft tree (or reports every
// validation failure found along the way, never just the first one). Domain stays the only source of
// truth for business rules -- every ConvertXxx method below calls a real Domain constructor and
// forwards whatever exception it throws; nothing here re-implements a Domain validation rule.
//
// Ligne en attente (the always-present "Add a ..." row's own draft, at every level: a SheetGenerationRuleDraft
// inside ExportProfileDraft, a Column/PointColumn/ApplicationColumn draft inside a
// SheetGenerationRuleDraft): a pending row that is still exactly as newly constructed (DraftJson.IsPristine)
// is silently ignored -- the common case, since almost every "Add" row is empty most of the time. A
// non-pristine, valid pending row is promoted into its owning list (as though "Add" had been clicked)
// and the pending slot is replaced with a fresh instance -- this is what removes defect A
// ("brouillon non validé") by construction: there is no code path left where a fully-typed-but-
// unclicked row is silently dropped. A non-pristine, invalid pending row blocks the whole
// conversion, its own draft carrying the failure.
public static class ExportProfileDraftMapper
{
    public static ExportProfileDraft FromDomain(ExportProfile profile) => new()
    {
        Id = profile.Id,
        Name = profile.Name,
        SheetRules = [.. profile.SheetRules.Select(FromDomainRule)],
    };

    private static SheetGenerationRuleDraft FromDomainRule(SheetGenerationRule rule) => new()
    {
        SheetName = rule.SheetName,
        PivotSource = rule.PivotSource,
        Columns = [.. rule.ColumnDefinitions.Select(FromDomainColumn)],
        PointColumns = [.. rule.PointColumnDefinitions.Select(FromDomainPointColumn)],
        ApplicationColumns = [.. rule.ApplicationColumnDefinitions.Select(FromDomainApplicationColumn)],
        // Untouched Domain objects, per this file's own header comment -- never wrapped in a draft.
        ConstantColumns = [.. rule.ConstantColumnDefinitions],
    };

    private static ColumnDefinitionDraft FromDomainColumn(ColumnDefinition column) => new()
    {
        Header = column.Header,
        SourceValue = column.Source?.ToString() ?? string.Empty,
    };

    private static PointColumnDefinitionDraft FromDomainPointColumn(PointColumnDefinition point) => new()
    {
        ColonneNom = point.ColonneNom,
        Header = point.Header,
        MarkValue = point.MarkValue,
    };

    private static ApplicationColumnDefinitionDraft FromDomainApplicationColumn(ApplicationColumnDefinition application) => new()
    {
        ApplicationNom = application.ApplicationNom,
        Header = application.Header,
        MarkValue = application.MarkValue,
    };

    // Whole-profile conversion -- walks every already-added sheet rule plus the still-pending
    // "Add a sheet rule" row, collecting every failure found anywhere in the tree rather than
    // stopping at the first one (so the page can show every broken field at once).
    public static ConversionResult<ExportProfile> ToDomain(ExportProfileDraft draft)
    {
        var errors = new List<DraftConversionError>();
        var rules = new List<SheetGenerationRule>();

        foreach (var ruleDraft in draft.SheetRules)
        {
            var result = ConvertSheetRule(ruleDraft);
            if (result.IsSuccess)
            {
                rules.Add(result.Value!);
            }
            else
            {
                errors.AddRange(result.Errors);
            }
        }

        if (!DraftJson.IsPristine(draft.PendingSheetRule))
        {
            var result = ConvertSheetRule(draft.PendingSheetRule);
            if (result.IsSuccess)
            {
                rules.Add(result.Value!);
                draft.SheetRules.Add(draft.PendingSheetRule);
                draft.PendingSheetRule = new SheetGenerationRuleDraft();
            }
            else
            {
                errors.AddRange(result.Errors);
            }
        }

        if (errors.Count > 0)
        {
            return ConversionResult<ExportProfile>.Failure(errors);
        }

        try
        {
            var profile = draft.Id.HasValue
                ? new ExportProfile(draft.Id.Value, draft.Name, rules)
                : new ExportProfile(draft.Name, rules);
            return ConversionResult<ExportProfile>.Success(profile);
        }
        catch (DomainValidationException ex)
        {
            return ConversionResult<ExportProfile>.Failure(draft, ex);
        }
    }

    // Single-rule conversion, reused both by an individual "Save changes"/"Add sheet rule" action and
    // by ToDomain's own per-rule loop above -- one validation path, per the ticket's explicit
    // requirement. Handles this rule's own 3 pending nested rows the same way ToDomain handles the
    // top-level pending sheet rule.
    public static ConversionResult<SheetGenerationRule> ConvertSheetRule(SheetGenerationRuleDraft draft)
    {
        var errors = new List<DraftConversionError>();

        var columns = ConvertList(draft.Columns, ConvertColumn, errors);
        PromoteIfNonPristineAndValid(draft.PendingColumn, ConvertColumn, columns, errors,
            () => { draft.Columns.Add(draft.PendingColumn); draft.PendingColumn = new ColumnDefinitionDraft(); });

        var pointColumns = ConvertList(draft.PointColumns, ConvertPointColumn, errors);
        PromoteIfNonPristineAndValid(draft.PendingPointColumn, ConvertPointColumn, pointColumns, errors,
            () => { draft.PointColumns.Add(draft.PendingPointColumn); draft.PendingPointColumn = new PointColumnDefinitionDraft(); });

        var applicationColumns = ConvertList(draft.ApplicationColumns, ConvertApplicationColumn, errors);
        PromoteIfNonPristineAndValid(draft.PendingApplicationColumn, ConvertApplicationColumn, applicationColumns, errors,
            () => { draft.ApplicationColumns.Add(draft.PendingApplicationColumn); draft.PendingApplicationColumn = new ApplicationColumnDefinitionDraft(); });

        if (errors.Count > 0)
        {
            return ConversionResult<SheetGenerationRule>.Failure(errors);
        }

        try
        {
            var rule = new SheetGenerationRule(
                draft.SheetName, draft.PivotSource, columns, pointColumns, applicationColumns, draft.ConstantColumns);
            return ConversionResult<SheetGenerationRule>.Success(rule);
        }
        catch (Exception ex) when (ex is DomainValidationException or DomainRuleViolationException)
        {
            return ConversionResult<SheetGenerationRule>.Failure(draft, ex);
        }
    }

    public static ConversionResult<ColumnDefinition> ConvertColumn(ColumnDefinitionDraft draft)
    {
        try
        {
            PivotFieldRef? source = string.IsNullOrEmpty(draft.SourceValue)
                ? null
                : Enum.Parse<PivotFieldRef>(draft.SourceValue);

            return ConversionResult<ColumnDefinition>.Success(new ColumnDefinition(draft.Header, source));
        }
        catch (DomainValidationException ex)
        {
            return ConversionResult<ColumnDefinition>.Failure(draft, ex);
        }
    }

    public static ConversionResult<PointColumnDefinition> ConvertPointColumn(PointColumnDefinitionDraft draft)
    {
        try
        {
            return ConversionResult<PointColumnDefinition>.Success(
                new PointColumnDefinition(draft.ColonneNom, draft.Header, draft.MarkValue));
        }
        catch (DomainValidationException ex)
        {
            return ConversionResult<PointColumnDefinition>.Failure(draft, ex);
        }
    }

    public static ConversionResult<ApplicationColumnDefinition> ConvertApplicationColumn(ApplicationColumnDefinitionDraft draft)
    {
        try
        {
            return ConversionResult<ApplicationColumnDefinition>.Success(
                new ApplicationColumnDefinition(draft.ApplicationNom, draft.Header, draft.MarkValue));
        }
        catch (DomainValidationException ex)
        {
            return ConversionResult<ApplicationColumnDefinition>.Failure(draft, ex);
        }
    }

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
