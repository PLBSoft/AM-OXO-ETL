namespace ExcelETL.Application.Identity;

public interface IUserRepository
{
    Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken cancellationToken = default);
}
