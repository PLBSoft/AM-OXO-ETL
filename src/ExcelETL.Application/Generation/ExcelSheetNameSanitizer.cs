namespace ExcelETL.Application.Generation;

// Defensive sheet-name sanitization for names derived from data the profile doesn't control -- today
// only TypeTacheMultipleCode (Lot T, dynamic per-code sheet generation), used verbatim as a sheet name.
// No known code (TM_PROC_MAD/TM_PROC_REL) violates Excel's constraints today, but a future client-added
// code could, without this lot being revisited. Hardcoded sheet names (Parents/Enfants) never go
// through this -- they're profile-authored strings, not runtime data.
public static class ExcelSheetNameSanitizer
{
    private const int MaxSheetNameLength = 31;
    private static readonly char[] ForbiddenCharacters = ['\\', '/', '?', '*', '[', ']', ':'];

    public static string Sanitize(string rawName)
    {
        ArgumentNullException.ThrowIfNull(rawName);

        var sanitized = rawName;
        foreach (var forbidden in ForbiddenCharacters)
        {
            sanitized = sanitized.Replace(forbidden, '_');
        }

        return sanitized.Length > MaxSheetNameLength ? sanitized[..MaxSheetNameLength] : sanitized;
    }
}
