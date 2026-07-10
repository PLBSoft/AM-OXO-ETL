using ExcelETL.Application.Extraction;

namespace Microsoft.AspNetCore.Routing;

// Interactive Blazor components cannot write raw file responses directly, so the archived
// workbook download is served by this plain minimal API endpoint instead. It inherits the
// app's global authentication fallback policy, so only signed-in admins can reach it.
internal static class AdminEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapGet("/history/{id:guid}/download", async (
            Guid id, IExtractionHistoryRepository extractionHistoryRepository) =>
        {
            var history = await extractionHistoryRepository.GetByIdAsync(id);

            if (history?.StoredFilePath is null || !File.Exists(history.StoredFilePath))
            {
                return Results.NotFound();
            }

            var bytes = await File.ReadAllBytesAsync(history.StoredFilePath);
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                Path.GetFileName(history.StoredFilePath));
        });
    }
}
