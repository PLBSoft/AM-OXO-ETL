namespace ExcelETL.BlazorAdmin.Configuration;

// Lot 038: bound from the "OxoApiTestClient" configuration section -- BaseUrl/ApiKey for the
// real HTTP call ApiTest.razor makes to POST /api/oxo/process. Per environment (see
// appsettings.Development.json), never hardcoded -- the whole point of this page is pointing at
// the real deployed Web API, not localhost, for post-deployment verification.
public class OxoApiTestClientOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
