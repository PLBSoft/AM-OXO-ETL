namespace ExcelETL.Application.Extraction.Oxo;

// Abstraction over DefaultProfileSeeder (Infrastructure) so a Razor component can depend on the
// Application layer only, consistent with every other admin-facing service in this codebase
// (IImportProfileStore, IUserManagementService, etc.) rather than reaching into Infrastructure
// directly. See DefaultProfileSeeder's own source comments for the full rationale behind
// SeedAsync's idempotence and the two Reset...ToDefaultAsync methods.
public interface IDefaultProfileSeeder
{
    // Identify the standard profiles by their stable seed Id, without a Razor component having to
    // reference the concrete Infrastructure type (DefaultProfileSeeder) itself, which the Clean
    // Architecture rule in this project's CLAUDE.md forbids for a controller or Razor component.
    Guid ImportProfileId { get; }

    Guid ExportProfileId { get; }

    Task SeedAsync(CancellationToken cancellationToken = default);

    // Discards the standard import profile (ImportProfiles.razor row keyed on
    // DefaultProfileSeeder.ImportProfileId) and recreates it from the current seed definition, in
    // one action -- so an admin who spots a stale/hand-edited standard profile after a deployment
    // that changed the seed doesn't have to manually reconcile it, or ask for the database to be
    // wiped. Any customization an admin made to this specific profile is lost; this is
    // deliberately a destructive, explicit action, never run automatically.
    Task ResetImportProfileToDefaultAsync(CancellationToken cancellationToken = default);

    // Same as ResetImportProfileToDefaultAsync, for the standard export profile
    // (DefaultProfileSeeder.ExportProfileId).
    Task ResetExportProfileToDefaultAsync(CancellationToken cancellationToken = default);
}
