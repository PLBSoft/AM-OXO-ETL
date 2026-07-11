using ExcelETL.Application.Extraction;
using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Persistence.Repositories;

// See ExtractionConfigRepository for why each method uses its own short-lived DbContext
// from the factory instead of a directly injected scoped one.
public class ExtractionHistoryRepository(IDbContextFactory<ExcelEtlDbContext> dbContextFactory)
    : IExtractionHistoryRepository
{
    public async Task AddAsync(ExtractionHistory history, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.ExtractionHistories.Add(history);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCompletedAsync(
        Guid historyId, string storedFilePath, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var history = await context.ExtractionHistories
            .FirstOrDefaultAsync(h => h.Id == historyId, cancellationToken)
            ?? throw new ExtractionHistoryNotFoundException(historyId);

        history.MarkCompleted(storedFilePath);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid historyId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var history = await context.ExtractionHistories
            .FirstOrDefaultAsync(h => h.Id == historyId, cancellationToken)
            ?? throw new ExtractionHistoryNotFoundException(historyId);

        history.MarkFailed();
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExtractionHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExtractionHistories.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ExtractionHistory>> GetAllOrderedByDateDescendingAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExtractionHistories
            .OrderByDescending(h => h.JobTimestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExtractionHistoryStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var totalJobs = await context.ExtractionHistories.CountAsync(cancellationToken);
        var pendingJobs = await context.ExtractionHistories
            .CountAsync(h => h.Status == ExtractionStatus.Pending, cancellationToken);
        var completedJobs = await context.ExtractionHistories
            .CountAsync(h => h.Status == ExtractionStatus.Completed, cancellationToken);
        var failedJobs = await context.ExtractionHistories
            .CountAsync(h => h.Status == ExtractionStatus.Failed, cancellationToken);

        // Averaging TimeSpan isn't translatable across every EF provider, so the small set of
        // completed-job timestamps is pulled into memory and averaged in C# instead.
        var completedTimestamps = await context.ExtractionHistories
            .Where(h => h.Status == ExtractionStatus.Completed && h.CompletedAtUtc != null)
            .Select(h => new { h.JobTimestamp, h.CompletedAtUtc })
            .ToListAsync(cancellationToken);

        TimeSpan? averageDuration = completedTimestamps.Count == 0
            ? null
            : TimeSpan.FromTicks(
                (long)completedTimestamps.Average(x => (x.CompletedAtUtc!.Value - x.JobTimestamp).Ticks));

        return new ExtractionHistoryStatistics(totalJobs, pendingJobs, completedJobs, failedJobs, averageDuration);
    }
}
