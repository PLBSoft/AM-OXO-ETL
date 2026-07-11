using ExcelETL.Application.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Diagnostics;

public class SystemLogRepository(IDbContextFactory<SystemLogsDbContext> dbContextFactory) : ISystemLogRepository
{
    public async Task<IReadOnlyList<SystemLogEntry>> GetRecentAsync(
        int count, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.SystemLogs
            .OrderByDescending(e => e.TimestampUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
