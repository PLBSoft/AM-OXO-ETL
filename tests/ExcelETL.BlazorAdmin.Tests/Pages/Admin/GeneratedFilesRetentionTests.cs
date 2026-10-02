using Bunit;
using ExcelETL.Application.Archiving;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Services;
using ExcelETL.Domain.Archiving;
using ExcelETL.Infrastructure.Archiving;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 090: retention setting + manual purge on /generated-files, admin only.
public class GeneratedFilesRetentionTests : BunitContext
{
    private readonly Mock<IGeneratedFileArchiveStore> _archiveStore = new();
    private readonly Mock<IArchiveRetentionSettingsStore> _settings = new();
    private readonly Mock<IGeneratedFilePurger> _purger = new();

    private static GeneratedFileRecord Record(bool purged = false, bool withTarget = true)
    {
        var r = new GeneratedFileRecord(
            Guid.NewGuid(), DateTime.UtcNow.AddDays(-100), withTarget ? "C7401" : null, "s.xlsx", "s-path",
            withTarget ? "t.xlsx" : null, withTarget ? "t-path" : null, Guid.NewGuid(), Guid.NewGuid(),
            withTarget ? GeneratedFileArchiveStatus.Success : GeneratedFileArchiveStatus.Rejected);
        if (purged)
        {
            r.MarkFilesPurged(DateTime.UtcNow);
        }

        return r;
    }

    private void Arrange(bool admin, int retentionDays = 90, params GeneratedFileRecord[] records)
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("someone");
        if (admin)
        {
            auth.SetRoles("Admin");
        }

        _archiveStore.Setup(s => s.SearchAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);
        _settings.Setup(s => s.GetRetentionDaysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(retentionDays);
        _purger.Setup(p => p.PurgeExpiredAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedFilePurgeResult(2, 3, 2 * 1024 * 1024));

        Services.AddSingleton(_archiveStore.Object);
        Services.AddSingleton(_settings.Object);
        Services.AddSingleton(_purger.Object);
        Services.AddSingleton(Mock.Of<ILocalTimeFormatter>());
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<IOptions<GeneratedFilesArchiveOptions>>(
            Options.Create(new GeneratedFilesArchiveOptions { RootPath = Path.GetTempPath() }));
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    [Fact]
    public void Panel_ForAdmin_ShowsTheCurrentRetentionValue()
    {
        Arrange(admin: true, retentionDays: 45);

        var cut = Render<GeneratedFiles>();

        cut.Find("#retention-days-input").GetAttribute("value").Should().Be("45");
    }

    [Fact]
    public void Panel_FieldAndButtons_ShareOneCompactRow_WithPlainBootstrapControls()
    {
        Arrange(admin: true);

        var cut = Render<GeneratedFiles>();

        // A plain form-control (not form-floating) has the same height as a plain btn, so the field and
        // both buttons line up; all three sit in the same grid row, Purge pushed to the right from md up.
        var input = cut.Find("#retention-days-input");
        input.ClassList.Should().Contain("form-control");
        input.Closest(".form-floating").Should().BeNull();
        cut.Find("label[for='retention-days-input']").Should().NotBeNull();

        var row = input.Closest(".row")!;
        row.QuerySelector("#save-retention-days-button").Should().NotBeNull();
        row.QuerySelector("#purge-now-button").Should().NotBeNull();

        input.ParentElement!.ClassList.Should().Contain(["col-4", "col-md-2", "col-xxl-1"]);
        cut.Find("#purge-now-button").ParentElement!.ClassList.Should().Contain("ms-md-auto");
        cut.Find("#save-retention-days-button").ClassList.Should().NotContain("btn-sm");
        cut.Find("#purge-now-button").ClassList.Should().NotContain("btn-sm");
    }

    [Fact]
    public void Panel_ForANonAdminAccount_IsAbsentFromTheDom_AndTheSettingIsNeverRead()
    {
        Arrange(admin: false);

        var cut = Render<GeneratedFiles>();

        cut.FindAll("#archive-retention-panel").Should().BeEmpty();
        cut.FindAll("#purge-now-button").Should().BeEmpty();
        _settings.Verify(s => s.GetRetentionDaysAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Save_PersistsTheNewValue()
    {
        Arrange(admin: true);
        var cut = Render<GeneratedFiles>();

        cut.Find("#retention-days-input").Input("30");
        cut.Find("#save-retention-days-button").Click();

        _settings.Verify(s => s.SaveRetentionDaysAsync(30, It.IsAny<CancellationToken>()), Times.Once);
        cut.Find("#purge-result").TextContent.Should().Contain("30");
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("99999")]
    public void Save_WithAnOutOfRangeValue_ShowsAnAlertAndSavesNothing(string value)
    {
        Arrange(admin: true);
        var cut = Render<GeneratedFiles>();

        cut.Find("#retention-days-input").Input(value);
        cut.Find("#save-retention-days-button").Click();

        cut.Find("#retention-error").GetAttribute("role").Should().Be("alert");
        _settings.Verify(s => s.SaveRetentionDaysAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void PurgeNow_NeedsAConfirmation_AndCancelDoesNotPurge()
    {
        Arrange(admin: true);
        var cut = Render<GeneratedFiles>();

        cut.Find("#purge-now-button").Click();
        cut.FindAll("#purge-now-confirm").Should().HaveCount(1);
        cut.Find("#cancel-purge-now-button").Click();

        cut.FindAll("#purge-now-confirm").Should().BeEmpty();
        _purger.Verify(p => p.PurgeExpiredAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void PurgeNow_Confirmed_RunsThePurge_ShowsTheResult_AndReloadsTheList()
    {
        Arrange(admin: true);
        var cut = Render<GeneratedFiles>();

        cut.Find("#purge-now-button").Click();
        cut.Find("#confirm-purge-now-button").Click();

        _purger.Verify(p => p.PurgeExpiredAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        cut.Find("#purge-result").TextContent.Should().Contain("2").And.Contain("3");
        _archiveStore.Verify(s => s.SearchAsync(null, It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public void PurgeNow_WhenRetentionIsZero_IsDisabled()
    {
        Arrange(admin: true, retentionDays: 0);

        var cut = Render<GeneratedFiles>();

        cut.Find("#purge-now-button").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void ARecordWhoseFilesWerePurged_ShowsExpiredInsteadOfDownloadButtons_AndStaysListed()
    {
        var purged = Record(purged: true);
        var fresh = Record();
        Arrange(admin: false, records: [purged, fresh]);

        var cut = Render<GeneratedFiles>();

        cut.Find($"#source-expired-{purged.Id}").TextContent.Should().NotBeEmpty();
        cut.Find($"#target-expired-{purged.Id}").Should().NotBeNull();
        cut.FindAll($"#prepare-download-source-button-{purged.Id}").Should().BeEmpty();
        cut.FindAll($"#prepare-download-source-button-{fresh.Id}").Should().HaveCount(1);
        cut.FindAll($"#source-expired-{fresh.Id}").Should().BeEmpty();
    }

    [Fact]
    public void ARejectedPurgedRecord_HasNoTargetExpiredNotice()
    {
        var purged = Record(purged: true, withTarget: false);
        Arrange(admin: false, records: [purged]);

        var cut = Render<GeneratedFiles>();

        cut.FindAll($"#source-expired-{purged.Id}").Should().HaveCount(1);
        cut.FindAll($"#target-expired-{purged.Id}").Should().BeEmpty();
    }
}
