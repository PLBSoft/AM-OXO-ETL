using ExcelETL.Domain.Entities;

namespace ExcelETL.Application.Extraction;

public interface IExtractionHistoryRepository
{
    Task AddAsync(ExtractionHistory history, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
