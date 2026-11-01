namespace ExcelETL.Infrastructure.Plans;

// Configuration section "PlanRendering" (lot 091, D12).
public sealed class PlanRenderingOptions
{
    public const string SectionName = "PlanRendering";

    public const string DefaultSofficePath = @"C:\Program Files\LibreOffice\program\soffice.exe";

    // Full path of LibreOffice's soffice executable on the server.
    public string SofficePath { get; set; } = DefaultSofficePath;

    // Maximum time for one sheet's conversion; the process is killed beyond it.
    public int TimeoutSeconds { get; set; } = 60;

    // LibreOffice user profile used by the service, kept between conversions (the first launch with a
    // new profile is much slower). Defaults to a folder under the system temp directory.
    public string? ProfileDirectory { get; set; }
}
