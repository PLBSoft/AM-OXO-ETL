namespace ExcelETL.Application.Diagnostics;

public interface ISystemLogRepository
{
    Task<IReadOnlyList<SystemLogEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
