namespace ExcelETL.BlazorAdmin.Services;

public interface IOxoApiTestClient
{
    Task<OxoApiTestResult> ProcessAsync(
        Guid importProfileId,
        Guid exportProfileId,
        Stream fileContent,
        string fileName,
        CancellationToken cancellationToken,
        string? username = null);

    Task<OxoApiHealthResult> GetHealthAsync(CancellationToken cancellationToken);
}
