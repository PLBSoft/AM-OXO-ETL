using ExcelETL.Application.Extraction;
using ExcelETL.Domain.Entities;

namespace ExcelETL.Infrastructure.Persistence.Repositories;

public class ExtractionHistoryRepository(ExcelEtlDbContext dbContext) : IExtractionHistoryRepository
{
    public async Task AddAsync(ExtractionHistory history, CancellationToken cancellationToken = default) =>
        await dbContext.ExtractionHistories.AddAsync(history, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
