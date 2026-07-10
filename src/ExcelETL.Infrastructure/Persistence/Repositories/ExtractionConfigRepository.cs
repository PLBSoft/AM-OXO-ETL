using ExcelETL.Application.Extraction;
using ExcelETL.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Persistence.Repositories;

public class ExtractionConfigRepository(ExcelEtlDbContext dbContext) : IExtractionConfigRepository
{
    public Task<ExtractionConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ExtractionConfigs
            .Include(config => config.Sheets)
            .ThenInclude(sheet => sheet.CellMappings)
            .FirstOrDefaultAsync(config => config.Id == id, cancellationToken);
}
