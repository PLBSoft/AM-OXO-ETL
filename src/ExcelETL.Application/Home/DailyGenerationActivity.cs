namespace ExcelETL.Application.Home;

// Lot 088 (088.2): one bar of the home page's activity chart -- files archived on one local day,
// counted per status.
public sealed record DailyGenerationActivity(DateOnly Day, int Success, int Warning, int Rejected)
{
    public int Total => Success + Warning + Rejected;
}
