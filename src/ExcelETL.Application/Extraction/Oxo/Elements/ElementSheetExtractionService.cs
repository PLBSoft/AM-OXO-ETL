using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo.Elements;

// Lot 084 (docs/tickets/tickets-tdd-lot-084-moteur-generique-feuilles-elements.md): the one engine
// of the five element sheets (ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES,
// DIVERS). Nothing here depends on the sheet: every difference between them lives in the profile.
//
// Per block read by IRepeatingBlockReader (optional fields read as ""):
// - repère = {repereEcho header}-{Identification}, a plain concatenation;
// - IsolementPivot from the known field names (ElementFieldNames), Designation/PositionALaPose ""
//   when absent;
// - couleur from the CouleurEtiquette block field when declared (filtered by
//   AllowedCouleursEtiquette), otherwise DefaultCouleurEtiquette;
// - unconditional Colonnes, then the point rules grouped by Colonne (OR within a group, one Point
//   per Colonne and block), evaluated on every field of the block;
// - a NoConditionalPointCreated warning (value: TypeElement) only when the sheet asks for it, has
//   point rules, and none ticked a Colonne.
public sealed class ElementSheetExtractionService(
    IRepeatingBlockReader repeatingBlockReader,
    IConditionalPointRuleEvaluator conditionalPointRuleEvaluator,
    IHeaderRuleResolver headerRuleResolver,
    ILogger<ElementSheetExtractionService> logger)
    : IElementSheetExtractionService
{
    public ElementSheetExtractionResult Extract(IWorkbookReader workbookReader, SheetExtractionRule sheetRule, string reperePrefix)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(sheetRule);

        var sheet = sheetRule.SheetName;
        EnsureBlockField(sheetRule, ElementFieldNames.Identification);
        EnsureBlockField(sheetRule, ElementFieldNames.TypeElement);

        var header = headerRuleResolver.Resolve(workbookReader, sheetRule, reperePrefix);
        if (!header.Fields.TryGetValue(ElementFieldNames.RepereEchoHeader, out var repereEcho))
        {
            throw new UnknownFieldReferenceException(ElementFieldNames.RepereEchoHeader);
        }

        var equipementRepere = repereEcho.Value ?? "";
        var zone = header.Fields.TryGetValue(ElementFieldNames.ZoneHeader, out var zoneField) ? zoneField.Value ?? "" : "";
        var hasCouleurField = sheetRule.Locator.Fields.Any(f => f.Name == ElementFieldNames.CouleurEtiquette);

        var blockResult = repeatingBlockReader.Read(sheetRule.Locator, workbookReader);
        var errors = new List<ExtractionError>(blockResult.Errors);
        foreach (var error in blockResult.Errors)
        {
            ExtractionErrorLogging.Log(logger, error);
        }

        var pointRuleGroups = sheetRule.PointRules.GroupBy(r => r.ColonneName).ToList();
        var noPointWarnings = DeduplicatedWarningTracker.ForNoConditionalPointCreated(sheet);
        var couleurWarnings = DeduplicatedWarningTracker.ForUnexpectedCouleurEtiquetteValue(sheet);
        var elements = new List<IsolementPivot>();
        var points = new List<PointPivot>();

        foreach (var block in blockResult.Blocks)
        {
            var fields = block.Fields;
            var repere = equipementRepere + "-" + fields[ElementFieldNames.Identification];
            var typeElement = fields[ElementFieldNames.TypeElement];
            if (string.IsNullOrWhiteSpace(typeElement))
            {
                // Only reachable when TypeElement was declared optional: the pivot needs one.
                var error = new ExtractionError(
                    sheet, block.StartRow.ToString(), ExtractionErrorCode.RequiredFieldMissing,
                    $"Block at row {block.StartRow} has required field(s) '{ElementFieldNames.TypeElement}' empty " +
                    $"while stop field '{sheetRule.Locator.StopFieldName}' is populated.");
                ExtractionErrorLogging.Log(logger, error);
                errors.Add(error);
                continue;
            }

            var couleurEtiquette = sheetRule.DefaultCouleurEtiquette ?? "";
            if (hasCouleurField)
            {
                var (value, unexpectedValue) = ResolveCouleur(
                    fields[ElementFieldNames.CouleurEtiquette], sheetRule.AllowedCouleursEtiquette);
                couleurEtiquette = value;
                if (unexpectedValue is not null)
                {
                    couleurWarnings.RecordIfNew(repere, unexpectedValue, logger, errors);
                }
            }

            elements.Add(new IsolementPivot(
                repere,
                fields.GetValueOrDefault(ElementFieldNames.Designation) ?? "",
                typeElement,
                fields.GetValueOrDefault(ElementFieldNames.PositionALaPose) ?? "",
                localisation: "",
                couleurEtiquette: couleurEtiquette,
                sourceSheetName: sheet));

            foreach (var colonneName in sheetRule.UnconditionalColonneNames)
            {
                points.Add(new PointPivot(colonneName, repere));
            }

            var tickedColonneCount = 0;
            foreach (var group in pointRuleGroups)
            {
                if (conditionalPointRuleEvaluator.Evaluate(group.ToList(), fields).ShouldCreatePoint)
                {
                    points.Add(new PointPivot(group.Key, repere));
                    tickedColonneCount++;
                }
            }

            if (sheetRule.WarnWhenNoConditionalPoint && pointRuleGroups.Count > 0 && tickedColonneCount == 0)
            {
                noPointWarnings.RecordIfNew(repere, typeElement, logger, errors);
            }
        }

        return new ElementSheetExtractionResult(elements, points, errors, zone);
    }

    // Same rule as the lot 068 / 2026-09-11 couleur resolver: blank -> "", no allowed list -> the
    // trimmed value, a match -> the allowed spelling, anything else -> "" plus a warning.
    private static (string Value, string? UnexpectedValue) ResolveCouleur(string rawValue, IReadOnlyList<string>? allowed)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return ("", null);
        }

        var trimmed = rawValue.Trim();
        if (allowed is null)
        {
            return (trimmed, null);
        }

        var match = allowed.FirstOrDefault(a => string.Equals(a.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        return match is not null ? (match, null) : ("", trimmed);
    }

    private static void EnsureBlockField(SheetExtractionRule sheetRule, string name)
    {
        if (sheetRule.Locator.Fields.All(f => f.Name != name))
        {
            throw new UnknownFieldReferenceException(name);
        }
    }
}
