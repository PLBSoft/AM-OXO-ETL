using ExcelETL.Application.Extraction;
using ExcelETL.Domain.Entities;
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
            ?? throw new InvalidOperationException($"Extraction history '{historyId}' was not found.");

        history.MarkCompleted(storedFilePath);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid historyId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var history = await context.ExtractionHistories
            .FirstOrDefaultAsync(h => h.Id == historyId, cancellationToken)
            ?? throw new InvalidOperationException($"Extraction history '{historyId}' was not found.");

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
}
