namespace ExcelETL.BlazorAdmin.Services;

public enum OxoApiTestResultStatus
{
    Success,
    BusinessRejection,
    ProfileNotFound,
    Unauthorized,
    TechnicalError,
    ConnectionError
}

public sealed record OxoApiTestRejectionError(string? Sheet, string? BlockIdentifier, string? Code, string? Message);

// Lot 038 (38.2): one variant per category OxoController can actually return, per the ticket's own
// explicit requirement -- ApiTest.razor switches on Status, never on a raw HttpStatusCode or a
// propagated exception. TechnicalError deliberately carries no message text of its own (only the raw
// status code, for logging/debugging, never rendered) -- the page shows a generic localized message
// for that variant, consistent with the project's "no raw stack trace/technical detail in the UI"
// principle applied elsewhere.
// ConnectionError (added post-delivery, 2026-07-26) is the one variant that never originates from an
// HTTP response at all -- it's the client-side "no TCP connection could be established" failure
// (server not running, wrong OxoApiTestClientOptions.BaseUrl, firewall) that OxoApiTestClient.
// ProcessAsync used to let propagate as a raw HttpRequestException, crashing the whole Blazor Server
// circuit instead of surfacing an inline message.
public sealed class OxoApiTestResult
{
    private OxoApiTestResult()
    {
    }

    public required OxoApiTestResultStatus Status { get; init; }

    public Stream? GeneratedFileContent { get; private init; }

    public string? GeneratedFileName { get; private init; }

    public IReadOnlyList<OxoApiTestRejectionError> RejectionErrors { get; private init; } = [];

    public string? ProfileNotFoundDetail { get; private init; }

    public int? HttpStatusCode { get; private init; }

    public static OxoApiTestResult Success(Stream generatedFileContent, string generatedFileName) => new()
    {
        Status = OxoApiTestResultStatus.Success,
        GeneratedFileContent = generatedFileContent,
        GeneratedFileName = generatedFileName
    };

    public static OxoApiTestResult BusinessRejection(IReadOnlyList<OxoApiTestRejectionError> errors) => new()
    {
        Status = OxoApiTestResultStatus.BusinessRejection,
        RejectionErrors = errors
    };

    public static OxoApiTestResult ProfileNotFound(string? detail) => new()
    {
        Status = OxoApiTestResultStatus.ProfileNotFound,
        ProfileNotFoundDetail = detail
    };

    public static OxoApiTestResult Unauthorized() => new() { Status = OxoApiTestResultStatus.Unauthorized };

    public static OxoApiTestResult TechnicalError(int? httpStatusCode) => new()
    {
        Status = OxoApiTestResultStatus.TechnicalError,
        HttpStatusCode = httpStatusCode
    };

    public static OxoApiTestResult ConnectionError() => new() { Status = OxoApiTestResultStatus.ConnectionError };
}
