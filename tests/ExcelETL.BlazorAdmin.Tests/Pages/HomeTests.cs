using Bunit;
using ExcelETL.Application.Home;
using ExcelETL.BlazorAdmin.Components.Pages;
using ExcelETL.BlazorAdmin.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages;

// Lot 054 (54.3/54.4/54.6): content only -- never routing or authorization (lots 049/051/052's own
// lesson, reaffirmed by this lot's own note d'efficacité). See HomeHttpTests.cs for the real HTTP
// requests proving "/" is reachable and reassigned.
public class HomeTests : BunitContext
{
    private readonly Mock<IHomeIndicatorsService> _serviceMock = new();
    private readonly Mock<ILocalTimeFormatter> _localTimeFormatterMock = new();

    // Resolved lazily (see the AddSingleton factory below), so
    // MobileBrand_RendersVersionInfo_WithATooltipWhenBuildDateKnown can still swap in a fixture
    // assembly by mutating this field -- registering a *new* service after SetRendererInfo below
    // (which resolves a service internally, locking the container, same as MainLayoutTests' own
    // documented ordering requirement) throws.
    private Func<ApplicationBuildInfo> _buildInfoFactory =
        () => new ApplicationBuildInfo(System.Reflection.Assembly.GetExecutingAssembly());

    public HomeTests()
    {
        Services.AddSingleton(_serviceMock.Object);
        Services.AddSingleton(_localTimeFormatterMock.Object);
        Services.AddLocalization();
        // Follow-up (post-062): Home.razor now also injects ApplicationBuildInfo for its mobile-only
        // client-logo/version footer.
        Services.AddSingleton(_ => _buildInfoFactory());

        // Lot 064: see LogsTests.cs's own constructor comment -- same RendererInfo requirement.
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    private static HomeIndicators KnownIndicators(
        int importProfileCount = 3, int exportProfileCount = 2, int generatedFileCount = 12) =>
        new(
            HomeIndicatorValue<int>.Known(importProfileCount),
            HomeIndicatorValue<int>.Known(exportProfileCount),
            HomeIndicatorValue<int>.Known(generatedFileCount),
            HomeIndicatorValue<DateTime?>.Known(new DateTime(2026, 7, 28, 10, 30, 0, DateTimeKind.Utc)));

    [Fact]
    public void WhileLoading_ShowsLoadingIndicator_NoTilesRendered()
    {
        var tcs = new TaskCompletionSource<HomeIndicators>();
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);

        var cut = Render<Home>();

        cut.Find("#home-loading");
        cut.FindAll("#home-kpi-import-profiles").Should().BeEmpty();
        cut.FindAll("#home-kpi-export-profiles").Should().BeEmpty();
        cut.FindAll("#home-kpi-generated-files").Should().BeEmpty();
        cut.FindAll("#home-kpi-last-generation").Should().BeEmpty();

        tcs.SetResult(KnownIndicators());
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);
    }

    [Fact]
    public void AriaLiveRegion_IsPresentFromInitialRender_BeforeDataFinishesLoading()
    {
        var tcs = new TaskCompletionSource<HomeIndicators>();
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);

        var cut = Render<Home>();

        var region = cut.Find("#home-kpi-region");
        region.GetAttribute("aria-live").Should().Be("polite");
    }

    [Fact]
    public void KnownValues_DisplaysFourTilesWithTheirValues()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(KnownIndicators(importProfileCount: 3, exportProfileCount: 2, generatedFileCount: 12));

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        cut.Find("#home-kpi-import-profiles").TextContent.Should().Contain("3");
        cut.Find("#home-kpi-export-profiles").TextContent.Should().Contain("2");
        cut.Find("#home-kpi-generated-files").TextContent.Should().Contain("12");
        cut.Find("#home-kpi-last-generation").TextContent.Should().Contain("28/07/2026");
    }

    [Fact]
    public void ZeroCountersAndAbsentLastGeneration_ShowsZeroAndAnExplicitNoGenerationState()
    {
        var indicators = new HomeIndicators(
            HomeIndicatorValue<int>.Known(0),
            HomeIndicatorValue<int>.Known(0),
            HomeIndicatorValue<int>.Known(0),
            HomeIndicatorValue<DateTime?>.Absent());
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(indicators);

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        cut.Find("#home-kpi-import-profiles").TextContent.Should().Contain("0");
        cut.Find("#home-kpi-export-profiles").TextContent.Should().Contain("0");
        cut.Find("#home-kpi-generated-files").TextContent.Should().Contain("0");
        // Not blank, not a default date -- an explicit, localized "no generation yet" state.
        cut.Find("#home-kpi-last-generation").TextContent.Should().NotBeNullOrWhiteSpace();
        cut.Find("#home-kpi-last-generation").TextContent.Should().NotContain("00:00");
    }

    [Fact]
    public void LinkTiles_PointToTheExpectedRoutes()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        cut.Find("#home-kpi-import-profiles").GetAttribute("href").Should().Be("import-profiles");
        cut.Find("#home-kpi-export-profiles").GetAttribute("href").Should().Be("export-profiles");
        cut.Find("#home-kpi-generated-files").GetAttribute("href").Should().Be("generated-files");
    }

    [Fact]
    public void LastGenerationTile_IsNotALink()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        cut.Find("#home-kpi-last-generation").TagName.Should().NotBe("A");
    }

    [Fact]
    public void OneUnavailableIndicator_ShowsDegradedStateOnThatTileOnly_OthersShowValues()
    {
        var indicators = new HomeIndicators(
            HomeIndicatorValue<int>.Known(3),
            HomeIndicatorValue<int>.Unavailable(),
            HomeIndicatorValue<int>.Known(12),
            HomeIndicatorValue<DateTime?>.Known(new DateTime(2026, 7, 28, 10, 0, 0, DateTimeKind.Utc)));
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(indicators);

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        cut.Find("#home-kpi-import-profiles").TextContent.Should().Contain("3");
        cut.Find("#home-kpi-export-profiles").TextContent.Should().NotContain("0");
        cut.Find("#home-kpi-generated-files").TextContent.Should().Contain("12");
        cut.Find("#home-kpi-last-generation").TextContent.Should().Contain("28/07/2026");
    }

    [Fact]
    public void ServiceCallThrows_PageRendersGlobalErrorMessage_WithoutExceptionPropagating()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = Render<Home>();

        cut.WaitForState(() => cut.FindAll("#home-global-error").Count == 1);
        cut.FindAll("#home-kpi-import-profiles").Should().BeEmpty();
    }

    [Fact]
    public void LabelIsAssociatedWithValue_ViaAriaLabelledby_NotJustVisualProximity()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        foreach (var tileId in new[] { "home-kpi-import-profiles", "home-kpi-export-profiles", "home-kpi-generated-files", "home-kpi-last-generation" })
        {
            var labelId = tileId + "-label";
            cut.Find("#" + labelId);
            var valueElement = cut.Find($"[aria-labelledby='{labelId}']");
            valueElement.Should().NotBeNull();
        }
    }

    [Fact]
    public void PageHasExactlyOneH1_MatchingPageTitle()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        HeadingHierarchyAssertions.AssertNoHeadingLevelSkip(cut);
    }

    // Mobile-first invariant (Lot V): Bootstrap's responsive grid handles narrow viewports on its
    // own -- exactly one instance of each tile in the DOM, no separate mobile template duplicating it.
    [Fact]
    public void EachTile_RendersExactlyOnce_NoDuplicateMobileTemplate()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        cut.FindAll("#home-kpi-import-profiles").Should().HaveCount(1);
        cut.FindAll("#home-kpi-export-profiles").Should().HaveCount(1);
        cut.FindAll("#home-kpi-generated-files").Should().HaveCount(1);
        cut.FindAll("#home-kpi-last-generation").Should().HaveCount(1);
    }

    // --- Follow-up (post-062): mobile-only client logo + version/date, since the sidebar hides its
    // own equivalent footer at that same viewport width (Home.razor.css, see
    // SidebarFooterMobileVisibilityTests.cs for the CSS-level visibility toggle itself). ------------

    [Fact]
    public void MobileBrand_RendersTheClientLogo_WithLocalizedAltText()
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
        try
        {
            _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());

            var cut = Render<Home>();

            var logo = cut.Find("#home-mobile-client-logo");
            logo.GetAttribute("src").Should().Be("images/client-logo.png");
            logo.GetAttribute("alt").Should().Be("Client logo");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void MobileBrand_RendersVersionInfo_WithATooltipWhenBuildDateKnown()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());
        var assemblyName = new System.Reflection.AssemblyName($"Lot062PostFixHomeFixture_{Guid.NewGuid():N}") { Version = new Version(1, 0, 3, 0) };
        var assemblyBuilder = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(assemblyName, System.Reflection.Emit.AssemblyBuilderAccess.Run);
        var metadataCtor = typeof(System.Reflection.AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!;
        assemblyBuilder.SetCustomAttribute(new System.Reflection.Emit.CustomAttributeBuilder(metadataCtor, ["BuildDate", "2026-07-30T16:49:02.0716047Z"]));
        assemblyBuilder.DefineDynamicModule(assemblyName.Name!);
        _buildInfoFactory = () => new ApplicationBuildInfo(assemblyBuilder);

        var cut = Render<Home>();

        var versionInfo = cut.Find("#home-mobile-version-info");
        versionInfo.TextContent.Should().Contain("v1.0.3.0");
        versionInfo.TextContent.Should().Contain("30/07/2026");
        versionInfo.GetAttribute("title").Should().Contain("16:49");
    }

    // Follow-up (post-064): mirrors NavMenuTests.cs's own
    // NavMenu_VersionInfo_UpdatesLabelAndTooltip_ToLocalTimeFormatterResult_OnceInteractive --
    // reopens Lot 064's own explicit exclusion of the build date once Simon hit the same UTC-vs-
    // local confusion here that Lot 064 had already fixed for the log table. Day-crossing stub
    // (23:49 UTC -> 01:49 local, next day) so both the compact label and the tooltip prove they
    // used the *converted* value, not the same UTC one under a different label.
    [Fact]
    public void MobileBrand_VersionInfo_UpdatesLabelAndTooltip_ToLocalTimeFormatterResult_OnceInteractive()
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
        try
        {
            _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());
            var assemblyName = new System.Reflection.AssemblyName($"Lot064PostFixHomeFixture_{Guid.NewGuid():N}") { Version = new Version(1, 0, 3, 0) };
            var assemblyBuilder = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(assemblyName, System.Reflection.Emit.AssemblyBuilderAccess.Run);
            var metadataCtor = typeof(System.Reflection.AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!;
            assemblyBuilder.SetCustomAttribute(new System.Reflection.Emit.CustomAttributeBuilder(metadataCtor, ["BuildDate", "2026-07-30T23:49:02.0000000Z"]));
            assemblyBuilder.DefineDynamicModule(assemblyName.Name!);
            _buildInfoFactory = () => new ApplicationBuildInfo(assemblyBuilder);
            _localTimeFormatterMock
                .Setup(f => f.FormatAsync(It.IsAny<DateTime>(), "yyyy-MM-dd HH:mm"))
                .ReturnsAsync("2026-07-31 01:49");
            SetRendererInfo(new RendererInfo("Server", isInteractive: true));

            var cut = Render<Home>();
            cut.WaitForState(() => cut.Find("#home-mobile-version-info").TextContent.Contains("31/07/2026"));

            cut.Find("#home-mobile-version-info").TextContent.Should().Contain("31/07/2026");
            cut.Find("#home-mobile-version-info").TextContent.Should().NotContain("30/07/2026");

            var title = cut.Find("#home-mobile-version-info").GetAttribute("title");
            title.Should().Contain("01:49");
            title.Should().Contain("Friday");
            title.Should().NotContain("Thursday");

            _localTimeFormatterMock.Verify(
                f => f.FormatAsync(It.IsAny<DateTime>(), "yyyy-MM-dd HH:mm"), Times.Once);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    // Lot 064 (64.2): same stable-id/UTC-fallback + interactive-conversion mechanism as
    // Logs.razor/GeneratedFiles.razor, for the one raw timestamp this page shows.
    [Fact]
    public void LastGenerationValue_HasStableId_AndShowsUtcFallback_WhenNotYetInteractive()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-last-generation-value").Count == 1);

        cut.Find("#home-kpi-last-generation-value").TextContent.Should().Be("28/07/2026 10:30");
    }

    [Fact]
    public void LastGenerationValue_UpdatesToTheLocalTimeFormatterResult_OnceInteractive()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());
        _localTimeFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<DateTime>(), "dd/MM/yyyy HH:mm"))
            .ReturnsAsync("STUBBED-LOCAL-TIME");
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));

        var cut = Render<Home>();
        cut.WaitForState(() => cut.Find("#home-kpi-last-generation-value").TextContent == "STUBBED-LOCAL-TIME");

        _localTimeFormatterMock.Verify(f => f.FormatAsync(It.IsAny<DateTime>(), "dd/MM/yyyy HH:mm"), Times.Once);
    }

    [Fact]
    public void MobileBrand_IsPresentEvenBeforeIndicatorsFinishLoading()
    {
        var tcs = new TaskCompletionSource<HomeIndicators>();
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);

        var cut = Render<Home>();

        cut.FindAll("#home-mobile-client-logo").Should().HaveCount(1);
        cut.FindAll("#home-mobile-version-info").Should().HaveCount(1);

        tcs.SetResult(KnownIndicators());
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);
    }
}
