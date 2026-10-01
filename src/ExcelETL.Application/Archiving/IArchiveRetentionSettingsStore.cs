namespace ExcelETL.Application.Archiving;

// How long archived files are kept (Lot 090). Stored in the shared database so the Web API (which
// purges) and BlazorAdmin (which edits) agree without a restart. When nothing was ever saved the
// default (ArchiveRetentionSetting.DefaultRetentionDays) applies; 0 disables the purge.
public interface IArchiveRetentionSettingsStore
{
    Task<int> GetRetentionDaysAsync(CancellationToken cancellationToken = default);

    Task SaveRetentionDaysAsync(int retentionDays, CancellationToken cancellationToken = default);
}
