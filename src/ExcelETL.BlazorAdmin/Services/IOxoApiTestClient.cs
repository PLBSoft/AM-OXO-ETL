namespace ExcelETL.BlazorAdmin.Services;

public interface IOxoApiTestClient
{
    Task<OxoApiTestResult> ProcessAsync(
        Guid importProfileId,
        Guid exportProfileId,
        Stream fileContent,
        string fileName,
        CancellationToken cancellationToken);
}
