namespace ExcelETL.Application.Home;

public interface IHomeIndicatorsService
{
    Task<HomeIndicators> GetIndicatorsAsync(CancellationToken cancellationToken = default);
}
