namespace ExcelETL.BlazorAdmin.Configuration;

// Lot 038 (38.1): fail-fast at startup, not a deferred exception on the first click of ApiTest.razor
// -- same "nothing silently degraded" principle already applied to ApiKeyAuthentication:ApiKey in
// ExcelETL.WebAPI/Program.cs. Extracted as a standalone static method (rather than inlined in
// Program.cs like that WebAPI precedent) specifically so it can be unit-tested directly: BlazorAdmin
// has no WebApplicationFactory-based integration test project to boot Program.cs against.
public static class OxoApiTestClientOptionsValidator
{
    public static void ValidateOrThrow(string? baseUrl, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Configuration value 'OxoApiTestClient:BaseUrl' must be set.");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Configuration value 'OxoApiTestClient:ApiKey' must be set.");
        }
    }
}
