using System.Globalization;
using System.Security.Claims;
using Bunit;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.Application.Identity;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 059 (59.7): harmonizes the delete-row buttons on the three list pages (ImportProfiles,
// ExportProfiles, Users) onto the exact same outline-red class string already used by the editors'
// own delete-row buttons (e.g. delete-default-tableau-button-{index}) -- token order included, so
// a strict-equality parity assertion is possible at all. Modify/Duplicate/Reset-password stay
// outline-secondary, on purpose: red only means something if it's the only red action.
public class ListPageDeleteButtonColorTests : BunitContext
{
    private const string EditorReferenceClass = "btn btn-sm btn-outline-danger block-field-icon-btn";
    private const string NonDestructiveClass = "btn btn-outline-secondary btn-sm block-field-icon-btn";

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }

    // --------------------------------------------------------------------------------------
    // ImportProfiles.razor
    // --------------------------------------------------------------------------------------

    private static ImportProfile BuildImportProfile(string name = "MAD OXO")
    {
        var locator = new RepeatingBlockLocator(
            "ISOLEMENT", firstBlockStartRow: 9, step: 7, stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var sheetRule = new SheetExtractionRule(
            "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"], [], []);
        return new ImportProfile(name, "MAD TRAVAUX", [], [], [sheetRule]);
    }

    [Fact]
    public void ImportProfiles_DeleteButtons_AreOutlineDanger_InBothTemplates() => WithCultureAsync(async () =>
    {
        var dbContextFactory = new TestDbContextFactory("ListPageDeleteButtonColorTests_Import_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();

        var profile = BuildImportProfile();
        await Services.GetRequiredService<IImportProfileStore>().SaveAsync(profile);

        var cut = Render<ImportProfiles>();

        cut.Find($"#delete-profile-button-{profile.Id}").GetAttribute("class").Should().Be(EditorReferenceClass);
        cut.Find($"#delete-profile-button-card-{profile.Id}").GetAttribute("class").Should().Be(EditorReferenceClass);
    });

    [Fact]
    public void ImportProfiles_ModifyAndDuplicateButtons_StayOutlineSecondary() => WithCultureAsync(async () =>
    {
        var dbContextFactory = new TestDbContextFactory("ListPageDeleteButtonColorTests_Import2_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();

        var profile = BuildImportProfile();
        await Services.GetRequiredService<IImportProfileStore>().SaveAsync(profile);

        var cut = Render<ImportProfiles>();

        foreach (var idPrefix in new[] { "edit-profile-button", "duplicate-profile-button" })
        {
            cut.Find($"#{idPrefix}-{profile.Id}").GetAttribute("class").Should().Be(NonDestructiveClass);
            cut.Find($"#{idPrefix}-card-{profile.Id}").GetAttribute("class").Should().Be(NonDestructiveClass);
        }
    });

    // --------------------------------------------------------------------------------------
    // ExportProfiles.razor
    // --------------------------------------------------------------------------------------

    private static ExportProfile BuildExportProfile(string name = "MAD OXO export") =>
        new(name,
            [
                new SheetGenerationRule(
                    "Parents", PivotSource.Equipement,
                    [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
                    [new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet")],
                    [])
            ]);

    [Fact]
    public void ExportProfiles_DeleteButtons_AreOutlineDanger_InBothTemplates() => WithCultureAsync(async () =>
    {
        var dbContextFactory = new TestDbContextFactory("ListPageDeleteButtonColorTests_Export_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();

        var profile = BuildExportProfile();
        await Services.GetRequiredService<IExportProfileStore>().SaveAsync(profile);

        var cut = Render<ExportProfiles>();

        cut.Find($"#delete-export-profile-button-{profile.Id}").GetAttribute("class").Should().Be(EditorReferenceClass);
        cut.Find($"#delete-export-profile-button-card-{profile.Id}").GetAttribute("class").Should().Be(EditorReferenceClass);
    });

    [Fact]
    public void ExportProfiles_ModifyAndDuplicateButtons_StayOutlineSecondary() => WithCultureAsync(async () =>
    {
        var dbContextFactory = new TestDbContextFactory("ListPageDeleteButtonColorTests_Export2_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();

        var profile = BuildExportProfile();
        await Services.GetRequiredService<IExportProfileStore>().SaveAsync(profile);

        var cut = Render<ExportProfiles>();

        foreach (var idPrefix in new[] { "edit-export-profile-button", "duplicate-export-profile-button" })
        {
            cut.Find($"#{idPrefix}-{profile.Id}").GetAttribute("class").Should().Be(NonDestructiveClass);
            cut.Find($"#{idPrefix}-card-{profile.Id}").GetAttribute("class").Should().Be(NonDestructiveClass);
        }
    });

    // --------------------------------------------------------------------------------------
    // Users.razor
    // --------------------------------------------------------------------------------------

    private static UserSummary MakeUser(string id, string email, string userName, string firstName = "First", string lastName = "Last") =>
        new(id, email, userName, firstName, lastName);

    private (Mock<IUserRepository> Repo, Mock<IUserManagementService> Mgmt) SetUpUsers(params UserSummary[] users)
    {
        var userRepositoryMock = new Mock<IUserRepository>();
        var userManagementServiceMock = new Mock<IUserManagementService>();

        userRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<UserSummary>)users);
        userManagementServiceMock.Setup(s => s.GetAdminUserIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<string>)[]);

        Services.AddSingleton(userRepositoryMock.Object);
        Services.AddSingleton(userManagementServiceMock.Object);
        Services.AddLocalization();

        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("current-user@example.com");
        authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, "current-user-id"));

        return (userRepositoryMock, userManagementServiceMock);
    }

    [Fact]
    public void Users_DeleteButton_IsOutlineDanger_InBothTemplates() => WithCulture("en-US", () =>
    {
        SetUpUsers(MakeUser("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();

        cut.Find("#delete-user-button-user-1").GetAttribute("class").Should().Be(EditorReferenceClass);
        cut.Find("#delete-user-button-card-user-1").GetAttribute("class").Should().Be(EditorReferenceClass);
    });

    [Fact]
    public void Users_ModifyAndResetPasswordButtons_StayOutlineSecondary() => WithCulture("en-US", () =>
    {
        SetUpUsers(MakeUser("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();

        foreach (var idPrefix in new[] { "edit-user-button", "reset-password-button" })
        {
            cut.Find($"#{idPrefix}-user-1").GetAttribute("class").Should().Be(NonDestructiveClass);
            cut.Find($"#{idPrefix}-card-user-1").GetAttribute("class").Should().Be(NonDestructiveClass);
        }
    });

    [Fact]
    public void Users_DisabledDeleteButton_IsStillOutlineDanger() => WithCulture("en-US", () =>
    {
        var (_, userManagementServiceMock) = SetUpUsers(MakeUser("current-user-id", "me@example.com", "me"));
        userManagementServiceMock.Setup(s => s.GetAdminUserIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<string>)[]);

        var cut = Render<Users>();

        var button = cut.Find("#delete-user-button-current-user-id");
        button.HasAttribute("disabled").Should().BeTrue();
        button.GetAttribute("class").Should().Be(EditorReferenceClass);
    });

    private void WithCultureAsync(Func<Task> action) =>
        WithCultureAsyncCore("en-US", action).GetAwaiter().GetResult();

    private static async Task WithCultureAsyncCore(string cultureName, Func<Task> action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { await action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }
}
