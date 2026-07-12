namespace ExcelETL.Application.Identity;

public interface IUserRepository
{
    Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UserProfile?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> UpdateProfileAsync(
        string id, string firstName, string lastName, string email, CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> ChangePasswordAsync(
        string id, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
