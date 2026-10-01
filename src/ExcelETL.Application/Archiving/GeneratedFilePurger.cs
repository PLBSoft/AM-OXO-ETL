using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Archiving;

public sealed class GeneratedFilePurger(
    IArchiveRetentionSettingsStore settingsStore,
    IGeneratedFileArchiveStore archiveStore,
    IGeneratedFileDeleter fileDeleter,
    ILogger<GeneratedFilePurger> logger) : IGeneratedFilePurger
{
    public async Task<GeneratedFilePurgeResult> PurgeExpiredAsync(
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var retentionDays = await settingsStore.GetRetentionDaysAsync(cancellationToken);
        if (retentionDays <= 0)
        {
            return GeneratedFilePurgeResult.None;
        }

        var cutoffUtc = nowUtc.AddDays(-retentionDays);
        var expired = await archiveStore.GetRecordsWithFilesOlderThanAsync(cutoffUtc, cancellationToken);

        int recordsPurged = 0, filesDeleted = 0;
        long bytesFreed = 0;
        foreach (var record in expired)
        {
            try
            {
                foreach (var path in new[] { record.SourceFilePath, record.TargetFilePath })
                {
                    if (path is null)
                    {
                        continue;
                    }

                    var freed = fileDeleter.Delete(path);
                    if (freed > 0)
                    {
                        filesDeleted++;
                        bytesFreed += freed;
                    }
                }

                await archiveStore.MarkFilesPurgedAsync(record.Id, nowUtc, cancellationToken);
                recordsPurged++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to purge archived files of record {RecordId} -- skipped", record.Id);
            }
        }

        logger.LogInformation(
            "Archive purge (retention {RetentionDays} days): {RecordsPurged} record(s), {FilesDeleted} file(s), {BytesFreed} byte(s) freed",
            retentionDays, recordsPurged, filesDeleted, bytesFreed);

        return new GeneratedFilePurgeResult(recordsPurged, filesDeleted, bytesFreed);
    }
}
