using ExcelETL.Application.Archiving;
using ExcelETL.Domain.Archiving;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Persistence.Repositories;

// Same short-lived-DbContext-per-method pattern as EfImportProfileStore/EfExportProfileStore.
// Append-only (no Update/Delete) -- see IGeneratedFileArchiveStore.
public class EfGeneratedFileArchiveStore(IDbContextFactory<ExcelEtlDbContext> dbContextFactory)
    : IGeneratedFileArchiveStore
{
    public async Task SaveAsync(GeneratedFileRecord record, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.GeneratedFileRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);
    }

    // The EquipementRepere Contains/OrdinalIgnoreCase filter is applied in-memory, not translated to
    // SQL: the EF Core InMemory provider's translation of string-comparison LINQ is unreliable (same
    // reasoning already applied in EfImportProfileStore.SaveAsync's own name-uniqueness check), and
    // the record volume this store handles is small by design (occasional client usage, no purge
    // policy yet but no expectation of a large table either -- see the ticket).
    public async Task<IReadOnlyList<GeneratedFileRecord>> SearchAsync(
        string? equipementRepere, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await context.GeneratedFileRecords.ToListAsync(cancellationToken);

        IEnumerable<GeneratedFileRecord> filtered = records;
        if (!string.IsNullOrWhiteSpace(equipementRepere))
        {
            var trimmed = equipementRepere.Trim();
            filtered = filtered.Where(r =>
                r.EquipementRepere is not null
                && r.EquipementRepere.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.OrderByDescending(r => r.GeneratedAtUtc).ToList();
    }

    public async Task<GeneratedFileRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.GeneratedFileRecords.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    // Lot 054 (54.2): a real aggregate query, not SearchAsync(null).Count -- this table has no purge
    // policy and its volume is expected to grow, unlike ImportProfile/ExportProfile's few dozen rows.
    public async Task<GeneratedFileArchiveSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var count = await context.GeneratedFileRecords.CountAsync(cancellationToken);
        var mostRecent = count == 0
            ? (DateTime?)null
            : await context.GeneratedFileRecords.MaxAsync(r => r.GeneratedAtUtc, cancellationToken);

        return new GeneratedFileArchiveSummary(count, mostRecent);
    }
}
