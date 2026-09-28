using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 089 (089.1): Chart.js is copied into the project (D1), like Bootstrap -- never loaded from a
// CDN, the client's on-prem server must not depend on any outside site. Checked as plain text, same
// convention as the rest of this folder. Any version bump goes through a ticket (licence re-checked).
public class ChartJsVendoredLibraryTests
{
    private static string RepoRoot { get; } = FindRepoRoot();

    private static string BlazorAdminPath(params string[] parts) =>
        Path.Combine([RepoRoot, "src", "ExcelETL.BlazorAdmin", .. parts]);

    [Fact]
    public void ChartJsLibrary_IsTheMitLicensedVersion4_5_1()
    {
        var header = File.ReadAllText(BlazorAdminPath("wwwroot", "lib", "chartjs", "chart.umd.min.js"))[..300];

        header.Should().Contain("Chart.js v4.5.1");
        header.Should().Contain("Released under the MIT License");
    }

    [Fact]
    public void ChartJsLicenceFile_IsShippedAlongsideTheLibrary()
    {
        File.ReadAllText(BlazorAdminPath("wwwroot", "lib", "chartjs", "LICENSE.md"))
            .Should().Contain("The MIT License");
    }

    [Fact]
    public void AppRazor_LoadsChartJsThroughAssets_NeverFromAnOutsideUrl()
    {
        var app = File.ReadAllText(BlazorAdminPath("Components", "App.razor"));

        app.Should().Contain("<script src=\"@Assets[\"lib/chartjs/chart.umd.min.js\"]\"></script>");
        app.Should().NotContain("cdn.jsdelivr.net");
        app.Should().NotContain("cdnjs.cloudflare.com");
    }

    [Fact]
    public void AppRazor_LoadsChartJsBeforeBlazor_SoItIsReadyWhenTheCircuitDrawsTheChart()
    {
        var app = File.ReadAllText(BlazorAdminPath("Components", "App.razor"));

        var chartJs = app.IndexOf("lib/chartjs/chart.umd.min.js", StringComparison.Ordinal);

        chartJs.Should().BeGreaterThanOrEqualTo(0);
        chartJs.Should().BeLessThan(app.IndexOf("_framework/blazor.web.js", StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExcelETL.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (ExcelETL.slnx).");
    }
}
