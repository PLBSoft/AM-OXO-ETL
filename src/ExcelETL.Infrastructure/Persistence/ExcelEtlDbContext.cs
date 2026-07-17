using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Persistence;

public class ExcelEtlDbContext(DbContextOptions<ExcelEtlDbContext> options) : DbContext(options)
{
    public DbSet<ExtractionConfig> ExtractionConfigs => Set<ExtractionConfig>();
    public DbSet<ExtractionHistory> ExtractionHistories => Set<ExtractionHistory>();
    public DbSet<ImportProfile> ImportProfiles => Set<ImportProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExcelEtlDbContext).Assembly);
    }
}
