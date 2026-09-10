namespace ExcelETL.BlazorAdmin.Services;

// Result of GET api/health -- deliberately just a flat DTO, not a multi-variant result like
// OxoApiTestResult: unlike ProcessAsync's several distinct HTTP outcomes (success/rejection/not
// found/unauthorized/technical error), this is a read-only diagnostic with exactly two states
// worth distinguishing on the page -- reachable (with data) or not.
public sealed class OxoApiHealthResult
{
    private OxoApiHealthResult(bool isReachable, string? status, string? version, string? database)
    {
        IsReachable = isReachable;
        Status = status;
        Version = version;
        Database = database;
    }

    public bool IsReachable { get; }

    public string? Status { get; }

    public string? Version { get; }

    public string? Database { get; }

    public static OxoApiHealthResult Reachable(string status, string version, string database) =>
        new(true, status, version, database);

    public static OxoApiHealthResult Unreachable() => new(false, null, null, null);
}
