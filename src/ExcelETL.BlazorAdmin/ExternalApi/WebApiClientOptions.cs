namespace ExcelETL.BlazorAdmin.ExternalApi;

// Bound from the "WebApiClient" configuration section. Configures the narrow, deliberate HTTP
// exception documented on ExcelProcessingClient -- not a general BlazorAdmin <-> WebAPI pattern.
public sealed class WebApiClientOptions
{
    public const string SectionName = "WebApiClient";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
