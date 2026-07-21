using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExcelETL.Hosting;

/// <summary>
/// Shared "apply pending EF Core migrations on startup" wiring, so this behavior is defined once
/// and both hosts (WebAPI, BlazorAdmin) call it the same way -- see CLAUDE.md, "Lot G4".
/// </summary>
public static class DatabaseMigrationHostExtensions
{
    /// <summary>
    /// Resolves <typeparamref name="TContext"/> via its registered <see cref="IDbContextFactory{TContext}"/>
    /// and applies any pending migrations, unless <c>Database:AutoMigrate</c> is set to <c>false</c>
    /// (default <c>true</c>) or the resolved context isn't backed by a relational provider.
    /// The <see cref="DbContext.Database"/>.IsRelational() check exists specifically so
    /// WebApplicationFactory-based integration tests that swap a context for the EF Core InMemory
    /// provider (rather than disabling this switch) don't hit "migrations not supported".
    /// Safe to call from multiple hosts pointed at the same database concurrently: EF Core's
    /// migration executor takes a distributed lock, so whichever host starts first applies the
    /// pending migrations and the other finds nothing left to do.
    /// </summary>
    public static async Task MigrateIfEnabledAsync<TContext>(
        this IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        if (!configuration.GetValue("Database:AutoMigrate", defaultValue: true))
        {
            return;
        }

        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TContext>>();
        await using var context = await factory.CreateDbContextAsync(cancellationToken);

        if (!context.Database.IsRelational())
        {
            return;
        }

        await context.Database.MigrateAsync(cancellationToken);
    }
}
