using ExcelETL.Application.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Identity;

public class UserRepository(IDbContextFactory<ApplicationIdentityDbContext> dbContextFactory) : IUserRepository
{
    public async Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Users
            .OrderBy(u => u.UserName)
            .Select(u => new UserSummary(u.Id, u.Email, u.UserName))
            .ToListAsync(cancellationToken);
    }
}
