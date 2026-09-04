using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Profile;

// Lot 067 (docs/tickets/tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md): a
// configurable mapping from a TacheMultiplePivot.TypeTacheMultipleCode value (e.g. "TM_PROC_MAD",
// itself already a hardcoded literal produced by ProcedureExtractionService.MapTypeTacheMultipleAlias)
// onto a target label the legacy app expects to see written into the "Colonne Travaux" export column
// (e.g. "Procédure MAD") -- client-confirmed to be genuinely configurable (a future client may use
// other labels), not a hardcoded switch in the generation engine. Code/Label share
// ImportProfile.MaxListItemNameLength -- the same bound already applied to DefaultTableaux/
// DefaultApplicationNames entries, no new constant needed for two more short strings.
public sealed record TacheMultipleTypeLabel
{
    public string Code { get; }
    public string Label { get; }

    public TacheMultipleTypeLabel(string code, string label)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainValidationException(
                "Code must not be empty.", nameof(code), DomainErrorCode.TacheMultipleTypeLabel_EmptyCode);
        }

        if (code.Trim().Length > ImportProfile.MaxListItemNameLength)
        {
            throw new DomainValidationException(
                $"Code must not exceed {ImportProfile.MaxListItemNameLength} characters.", nameof(code),
                DomainErrorCode.TacheMultipleTypeLabel_CodeTooLong, ImportProfile.MaxListItemNameLength);
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainValidationException(
                "Label must not be empty.", nameof(label), DomainErrorCode.TacheMultipleTypeLabel_EmptyLabel);
        }

        if (label.Trim().Length > ImportProfile.MaxListItemNameLength)
        {
            throw new DomainValidationException(
                $"Label must not exceed {ImportProfile.MaxListItemNameLength} characters.", nameof(label),
                DomainErrorCode.TacheMultipleTypeLabel_LabelTooLong, ImportProfile.MaxListItemNameLength);
        }

        Code = code;
        Label = label;
    }
}
