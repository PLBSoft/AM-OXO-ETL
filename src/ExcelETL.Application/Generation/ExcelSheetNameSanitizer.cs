using ExcelETL.Domain.Generation.Profile;

namespace ExcelETL.Application.Generation;

// Defensive sheet-name sanitization for names derived from data the profile doesn't control -- today
// only TypeTacheMultipleCode (Lot T, dynamic per-code sheet generation), used verbatim as a sheet name.
// No known code (TM_PROC_MAD/TM_PROC_REL) violates Excel's constraints today, but a future client-added
// code could, without this lot being revisited. Hardcoded sheet names (Parents/Enfants) never go
// through this -- they're profile-authored strings, not runtime data.
// Lot 080.5 (D6): leading/trailing apostrophes are removed too, and a name left empty becomes "_". The rules
// come from ExcelSheetName (domain). Two codes sanitized to the same name are caught afterwards by
// GeneratedSheetNameValidator, not here.
public static class ExcelSheetNameSanitizer
{
    private const string EmptyNameReplacement = "_";

    public static string Sanitize(string rawName)
    {
        ArgumentNullException.ThrowIfNull(rawName);

        var sanitized = rawName;
        foreach (var forbidden in ExcelSheetName.ForbiddenCharacters)
        {
            sanitized = sanitized.Replace(forbidden, '_');
        }

        sanitized = sanitized.Trim('\'');
        if (sanitized.Length > ExcelSheetName.MaxLength)
        {
            sanitized = sanitized[..ExcelSheetName.MaxLength].TrimEnd('\'');
        }

        return sanitized.Length == 0 ? EmptyNameReplacement : sanitized;
    }
}
