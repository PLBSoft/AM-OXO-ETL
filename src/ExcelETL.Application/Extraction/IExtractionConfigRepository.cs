using ExcelETL.Domain.Entities;

namespace ExcelETL.Application.Extraction;

public interface IExtractionConfigRepository
{
    Task<ExtractionConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExtractionConfig>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(ExtractionConfig config, CancellationToken cancellationToken = default);

    Task AddSheetAsync(Guid configId, SheetConfig sheet, CancellationToken cancellationToken = default);

    Task AddCellMappingAsync(
        Guid configId, Guid sheetId, CellMapping mapping, CancellationToken cancellationToken = default);
}
