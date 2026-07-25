namespace ExcelETL.BlazorAdmin.Services;

public enum OxoApiTestResultStatus
{
    Success,
    BusinessRejection,
    ProfileNotFound,
    Unauthorized,
    TechnicalError
}

public sealed record OxoApiTestRejectionError(string? Sheet, string? BlockIdentifier, string? Code, string? Message);

// Lot 038 (38.2): one variant per category OxoController can actually return, per the ticket's own
// explicit requirement -- ApiTest.razor switches on Status, never on a raw HttpStatusCode or a
// propagated exception. TechnicalError deliberately carries no message text of its own (only the raw
// status code, for logging/debugging, never rendered) -- the page shows a generic localized message
// for that variant, consistent with the project's "no raw stack trace/technical detail in the UI"
// principle applied elsewhere.
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
}
