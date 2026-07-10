using ExcelETL.Domain.Entities;

namespace ExcelETL.Application.Extraction;

public interface IExtractionConfigRepository
{
    Task<ExtractionConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
