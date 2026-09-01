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
// propagated exception. TechnicalError optionally carries the short exception type name + message
// the WebAPI's GlobalExceptionHandler now surfaces for any unmapped exception (Lot 065) -- never a
// stack trace, that response body property doesn't exist. Both stay null when the response body
// wasn't a parsable ProblemDetails carrying them (e.g. an empty body) -- the page falls back to its
// pre-existing generic localized message in that case, exactly as before Lot 065.
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

    public string? ExceptionType { get; private init; }

    public string? ExceptionMessage { get; private init; }

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

    public static OxoApiTestResult TechnicalError(
        int? httpStatusCode, string? exceptionType = null, string? exceptionMessage = null) => new()
    {
        Status = OxoApiTestResultStatus.TechnicalError,
        HttpStatusCode = httpStatusCode,
        ExceptionType = exceptionType,
        ExceptionMessage = exceptionMessage
    };

    public static OxoApiTestResult ConnectionError() => new() { Status = OxoApiTestResultStatus.ConnectionError };
}
