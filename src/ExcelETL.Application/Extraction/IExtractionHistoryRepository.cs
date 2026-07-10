using ExcelETL.Domain.Entities;

namespace ExcelETL.Application.Extraction;

public interface IExtractionHistoryRepository
{
    Task AddAsync(ExtractionHistory history, CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(Guid historyId, string storedFilePath, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(Guid historyId, CancellationToken cancellationToken = default);

    Task<ExtractionHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExtractionHistory>> GetAllOrderedByDateDescendingAsync(
        CancellationToken cancellationToken = default);
}
