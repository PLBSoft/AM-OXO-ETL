using ExcelETL.Application.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Diagnostics;

// Serilog.Sinks.MSSqlServer owns and auto-creates the physical schema of the SystemLogs table
// (see the UseSerilog configuration in both hosts' Program.cs) -- this DbContext only ever
// reads from it and intentionally carries no migrations of its own.
public class SystemLogsDbContext(DbContextOptions<SystemLogsDbContext> options) : DbContext(options)
{
    public DbSet<SystemLogEntry> SystemLogs => Set<SystemLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemLogEntry>(entity =>
        {
            entity.ToTable("SystemLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.TimestampUtc).HasColumnName("TimeStamp");
            entity.Property(e => e.Level).HasColumnName("Level");
            entity.Property(e => e.Message).HasColumnName("Message");
            entity.Property(e => e.Exception).HasColumnName("Exception");
        });
    }
}
