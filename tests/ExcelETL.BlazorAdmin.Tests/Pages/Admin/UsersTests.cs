using System.Globalization;
using Bunit;
using ExcelETL.Application.Identity;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests.Pages;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

public class UsersTests : BunitContext
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();

    public UsersTests()
    {
        Services.AddSingleton(_userRepositoryMock.Object);
        Services.AddLocalization();
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void Users_WithNoUsers_DisplaysNoEntriesMessage() => WithCulture("en-US", () =>
    {
        _userRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<UserSummary>)[]);

        var cut = Render<Users>();

        cut.Markup.Should().Contain("No users found.");
    });

    // Lot 042 (42.2): the mobile card's per-row title previously skipped straight from h1 to h5 --
    // fixed to h2, keeping its pre-existing visual size via the Bootstrap `.h5` utility class.
    [Fact]
    public void Users_WithExistingUser_HasNoHeadingLevelSkip() => WithCulture("en-US", () =>
    {
        _userRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<UserSummary>)[new UserSummary("user-1", "alice@example.com", "alice")]);

        var cut = Render<Users>();

        HeadingHierarchyAssertions.AssertNoHeadingLevelSkip(cut);
    });

    // V2: mobile-first table -> card fallback at the md breakpoint, same idiom as
    // ImportProfiles/ExportProfiles -- see ImportProfilesTests for the fuller rationale comment.
    [Fact]
    public void Users_RendersBothTableAndCardTemplates_WithResponsiveClasses() => WithCulture("en-US", () =>
    {
        _userRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<UserSummary>)[new UserSummary("user-1", "alice@example.com", "alice")]);

        var cut = Render<Users>();

        var table = cut.Find("table.table");
        table.ClassList.Should().Contain("d-none");
        table.ClassList.Should().Contain("d-md-table");

        var cardContainer = cut.Find("div.d-md-none");
        cardContainer.QuerySelectorAll(".card").Should().HaveCount(1);
    });

    [Fact]
    public void Users_CardTemplate_DisplaysSameContentAsTable() => WithCulture("en-US", () =>
    {
        _userRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<UserSummary>)[new UserSummary("user-1", "alice@example.com", "alice")]);

        var cut = Render<Users>();

        var card = cut.Find("div.d-md-none .card");
        card.TextContent.Should().Contain("alice@example.com");
        card.TextContent.Should().Contain("alice");
        card.TextContent.Should().Contain("user-1");
    });
}
