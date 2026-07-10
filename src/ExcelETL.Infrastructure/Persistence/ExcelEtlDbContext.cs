using ExcelETL.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Persistence;

public class ExcelEtlDbContext(DbContextOptions<ExcelEtlDbContext> options) : DbContext(options)
{
    public DbSet<ExtractionConfig> ExtractionConfigs => Set<ExtractionConfig>();
    public DbSet<ExtractionHistory> ExtractionHistories => Set<ExtractionHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExcelEtlDbContext).Assembly);
    }
}
