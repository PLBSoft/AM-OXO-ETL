using ExcelETL.Application.Archiving;
using ExcelETL.Domain.Archiving;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Persistence.Repositories;

// Single-row store (ArchiveRetentionSetting.SingletonId), same short-lived-DbContext-per-method
// pattern as the other stores. No row yet means the default applies -- nothing is seeded.
public class EfArchiveRetentionSettingsStore(IDbContextFactory<ExcelEtlDbContext> dbContextFactory)
    : IArchiveRetentionSettingsStore
{
    public async Task<int> GetRetentionDaysAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var setting = await context.ArchiveRetentionSettings
            .FirstOrDefaultAsync(s => s.Id == ArchiveRetentionSetting.SingletonId, cancellationToken);
        return setting?.RetentionDays ?? ArchiveRetentionSetting.DefaultRetentionDays;
    }

    public async Task SaveRetentionDaysAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var setting = await context.ArchiveRetentionSettings
            .FirstOrDefaultAsync(s => s.Id == ArchiveRetentionSetting.SingletonId, cancellationToken);

        if (setting is null)
        {
            context.ArchiveRetentionSettings.Add(new ArchiveRetentionSetting(retentionDays));
        }
        else
        {
            setting.ChangeRetentionDays(retentionDays);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
