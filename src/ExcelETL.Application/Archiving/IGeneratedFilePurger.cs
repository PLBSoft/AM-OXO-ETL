namespace ExcelETL.Application.Archiving;

// Deletes the files (never the history entries) of every archived record older than the configured
// retention (IArchiveRetentionSettingsStore). Best-effort: never throws for a file/record that
// cannot be purged, it is logged and skipped.
public interface IGeneratedFilePurger
{
    Task<GeneratedFilePurgeResult> PurgeExpiredAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
