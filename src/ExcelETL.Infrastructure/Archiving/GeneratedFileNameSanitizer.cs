namespace ExcelETL.Infrastructure.Archiving;

// Defensive sanitization of the original uploaded file name before it's embedded into an archived
// file's on-disk name -- same defensive principle as ExcelSheetNameSanitizer (Lot T4), applied here
// to a Windows file name rather than an Excel sheet name, so a different forbidden-character set
// (per docs/tickets-tdd-lot-034-archivage-fichiers-generes-api.md, 34.3). No length cap: unlike an
// Excel sheet name, Windows file names have no comparably tight limit worth enforcing defensively
// here.
public static class GeneratedFileNameSanitizer
{
    private static readonly char[] ForbiddenCharacters = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    public static string Sanitize(string rawFileName)
    {
        ArgumentNullException.ThrowIfNull(rawFileName);

        var sanitized = rawFileName;
        foreach (var forbidden in ForbiddenCharacters)
        {
            sanitized = sanitized.Replace(forbidden, '_');
        }

        return sanitized;
    }
}
