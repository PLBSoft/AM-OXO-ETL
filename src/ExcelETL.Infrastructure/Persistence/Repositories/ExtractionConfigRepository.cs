using ExcelETL.Application.Extraction;
using ExcelETL.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Persistence.Repositories;

// Each method owns a short-lived DbContext created via the factory, rather than depending on
// an injected scoped DbContext: WebAPI consumes this per-request (where scoped would be fine
// too), but Blazor Server's interactive circuits are long-lived and can invoke handlers
// concurrently, so a directly injected DbContext would be unsafe to share across them.
public class ExtractionConfigRepository(IDbContextFactory<ExcelEtlDbContext> dbContextFactory)
    : IExtractionConfigRepository
{
    public async Task<ExtractionConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExtractionConfigs
            .Include(config => config.Sheets)
            .ThenInclude(sheet => sheet.CellMappings)
            .FirstOrDefaultAsync(config => config.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ExtractionConfig>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExtractionConfigs
            .Include(config => config.Sheets)
            .ThenInclude(sheet => sheet.CellMappings)
            .OrderBy(config => config.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ExtractionConfig config, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.ExtractionConfigs.Add(config);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddSheetAsync(Guid configId, SheetConfig sheet, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var config = await context.ExtractionConfigs
            .Include(c => c.Sheets)
            .FirstOrDefaultAsync(c => c.Id == configId, cancellationToken)
            ?? throw new ExtractionConfigNotFoundException(configId);

        config.AddSheet(sheet);

        // The Domain assigns a non-default Guid client-side (Entity's constructor), so EF Core's
        // change tracker cannot tell "new entity with a pre-assigned key" apart from "existing
        // entity being modified" via DetectChanges alone -- it defaults to Modified. Since we
        // just added this child via a plain collection mutation (not context.Add), state it explicitly.
        context.Entry(sheet).State = EntityState.Added;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddCellMappingAsync(
        Guid configId, Guid sheetId, CellMapping mapping, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var config = await context.ExtractionConfigs
            .Include(c => c.Sheets)
            .ThenInclude(s => s.CellMappings)
            .FirstOrDefaultAsync(c => c.Id == configId, cancellationToken)
            ?? throw new ExtractionConfigNotFoundException(configId);

        var sheet = config.Sheets.FirstOrDefault(s => s.Id == sheetId)
            ?? throw new SheetNotFoundInExtractionConfigException(configId, sheetId);

        sheet.AddCellMapping(mapping);

        // See AddSheetAsync for why this must be stated explicitly.
        context.Entry(mapping).State = EntityState.Added;

        await context.SaveChangesAsync(cancellationToken);
    }
}
