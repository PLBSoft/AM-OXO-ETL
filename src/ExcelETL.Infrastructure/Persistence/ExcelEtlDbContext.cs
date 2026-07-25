using ExcelETL.Domain.Archiving;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Profile;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Persistence;

public class ExcelEtlDbContext(DbContextOptions<ExcelEtlDbContext> options) : DbContext(options)
{
    public DbSet<ImportProfile> ImportProfiles => Set<ImportProfile>();
    public DbSet<ExportProfile> ExportProfiles => Set<ExportProfile>();
    public DbSet<GeneratedFileRecord> GeneratedFileRecords => Set<GeneratedFileRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExcelEtlDbContext).Assembly);
    }
}
