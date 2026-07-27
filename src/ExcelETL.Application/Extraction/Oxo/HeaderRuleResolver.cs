using System.Globalization;
using System.Text.RegularExpressions;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.Application.Extraction.Oxo;

// Lot 047 (docs/reference/spec-migration-entetes-profile-driven-directcell.md §3): resolves every
// HeaderFieldRule/HeaderCompositeRule of a SheetExtractionRule against the real workbook. This is the
// profile-driven replacement for the coordinates + transformation logic previously hardcoded in the
// per-sheet extraction services -- see ProcedureExtractionService/AutresJointsTouchesExtractionService/
// DiversExtractionService.
//
// Deliberately policy-free about "required": whether a blank/unresolvable field rejects the whole
// file (and with which ExtractionErrorCode) is the calling sheet service's own business decision, not
// this resolver's -- it just reports RawValue/Value/ErrorMessage per field and lets the caller decide.
public sealed partial class HeaderRuleResolver(ITextTransformEvaluator textTransformEvaluator) : IHeaderRuleResolver
{
    // Input date parsing stays a fixed, hardcoded format list (out of this lot's scope per spec §5/§6 --
    // only the *output* DateFormat is profile-driven) -- the same list ProcedureExtractionService's own
    // TryParseDate already used before this lot, now centralized here since every HeaderFieldRule with a
    // DateFormat needs it.
    private static readonly string[] DateInputFormats = ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy"];

    public HeaderResolutionResult Resolve(IWorkbookReader workbookReader, SheetExtractionRule sheetRule, string reperePrefix)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(sheetRule);
        ArgumentNullException.ThrowIfNull(reperePrefix);

        var fields = new Dictionary<string, HeaderFieldResolution>();
        foreach (var field in sheetRule.HeaderFields)
        {
            var rawValue = workbookReader.ReadCellValue(field.Cell.Sheet, field.Cell.Range);
            fields[field.Name] = ResolveField(field, rawValue, reperePrefix);
        }

        var composites = new Dictionary<string, string?>();
        foreach (var composite in sheetRule.HeaderComposites)
        {
            composites[composite.Name] = SubstituteTemplate(composite.Template, fields);
        }

        return new HeaderResolutionResult(fields, composites);
    }

    private HeaderFieldResolution ResolveField(HeaderFieldRule field, string? rawValue, string reperePrefix)
    {
        var value = rawValue;

        if (field.StripReperePrefix)
        {
            var (stripped, error) = textTransformEvaluator.Evaluate(
                new SubstringAfter(reperePrefix), value, new Dictionary<string, string>());
            if (error is not null)
            {
                return new HeaderFieldResolution(field.Name, rawValue, null, error);
            }

            value = stripped;
        }

        if (field.DateFormat is not null)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !DateTime.TryParseExact(
                    value, DateInputFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return new HeaderFieldResolution(
                    field.Name, rawValue, null, $"Value '{value}' is not a recognizable date.");
            }

            value = parsedDate.ToString(field.DateFormat, CultureInfo.InvariantCulture);
        }

        return new HeaderFieldResolution(field.Name, rawValue, value, null);
    }

    // Domain-level cross-validation (SheetExtractionRule's own constructor) already guarantees every
    // placeholder references a real HeaderFieldRule.Name on the same sheet -- this is a defensive
    // second check (same "profile/configuration bug" precedent as Concat/FieldRef's
    // UnknownFieldReferenceException), reachable only if a composite is ever resolved independently of
    // that construction-time guarantee. A field that resolved to null (blank cell, failed transform)
    // substitutes as an empty string rather than propagating an error -- the caller who actually needs
    // that field's own error (e.g. PROCEDURE's whole-file rejection) inspects Fields directly and never
    // reaches the composite in that case.
    private static string SubstituteTemplate(string template, IReadOnlyDictionary<string, HeaderFieldResolution> fields) =>
        PlaceholderPattern().Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            if (!fields.TryGetValue(name, out var field))
            {
                throw new UnknownFieldReferenceException(name);
            }

            return field.Value ?? "";
        });

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex PlaceholderPattern();
}
