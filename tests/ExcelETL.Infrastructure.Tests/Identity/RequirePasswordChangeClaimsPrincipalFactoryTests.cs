using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Identity;

// Lot 045 (45.0/45.4).
public class RequirePasswordChangeClaimsPrincipalFactoryTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityManagerMocks.CreateUserManagerMock();

    private RequirePasswordChangeClaimsPrincipalFactory CreateFactory() => new(
        _userManagerMock.Object,
        IdentityManagerMocks.CreateRoleManagerMock().Object,
        Options.Create(new IdentityOptions()));

    private void SetUpUserIdentifiers(ApplicationUser user)
    {
        _userManagerMock.Setup(m => m.GetUserIdAsync(user)).ReturnsAsync(user.Id);
        _userManagerMock.Setup(m => m.GetUserNameAsync(user)).ReturnsAsync(user.UserName);
    }

    // GenerateClaimsAsync itself stays `protected override` (can't widen access on an override) --
    // exercised through the public CreateAsync(user) entry point instead, same as SignInManager does.
    [Fact]
    public async Task CreateAsync_UserWithFlagTrue_PrincipalHasClaim()
    {
        var user = new ApplicationUser { Id = "1", UserName = "alice", RequirePasswordChangeOnFirstLogin = true };
        SetUpUserIdentifiers(user);
        var factory = CreateFactory();

        var principal = await factory.CreateAsync(user);

        principal.HasClaim(RequirePasswordChangeClaimsPrincipalFactory.ClaimType, bool.TrueString).Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_UserWithFlagFalse_PrincipalDoesNotHaveClaim()
    {
        var user = new ApplicationUser { Id = "1", UserName = "alice", RequirePasswordChangeOnFirstLogin = false };
        SetUpUserIdentifiers(user);
        var factory = CreateFactory();

        var principal = await factory.CreateAsync(user);

        principal.HasClaim(c => c.Type == RequirePasswordChangeClaimsPrincipalFactory.ClaimType).Should().BeFalse();
    }
}
