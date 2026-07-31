using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// App.razor's <head> ordering determines whether the dark-mode script avoids a flash of the wrong
// theme -- theme.js must run (and therefore be referenced) before any stylesheet is parsed. Not
// something an HTTP/bUnit test can meaningfully assert (script execution order isn't observable
// without a real browser), so this reads App.razor as plain text, same convention as the other
// Styling/ tests that check file content rather than rendered/computed output.
public class ThemeToggleAppHeadOrderTests
{
    private static string AppRazor { get; } = File.ReadAllText(AppRazorPath());

    [Fact]
    public void ThemeScript_IsReferenced_BeforeEveryStylesheetLink()
    {
        var scriptIndex = AppRazor.IndexOf("js/theme.js", StringComparison.Ordinal);
        scriptIndex.Should().BeGreaterThanOrEqualTo(0, "App.razor should reference wwwroot/js/theme.js");

        var stylesheetMarkers = new[]
        {
            "bootstrap.min.css",
            "css/theme-m3.css",
            "app.css",
        };

        foreach (var marker in stylesheetMarkers)
        {
            var stylesheetIndex = AppRazor.IndexOf(marker, StringComparison.Ordinal);
            stylesheetIndex.Should().BeGreaterThanOrEqualTo(0, $"App.razor should reference {marker}");
            scriptIndex.Should().BeLessThan(stylesheetIndex, $"theme.js must run before {marker} loads to avoid a theme flash");
        }
    }

    [Fact]
    public void ThemeScript_HasNoDeferOrAsyncOrModuleType_SoItRunsSynchronously()
    {
        var scriptTagStart = AppRazor.IndexOf("js/theme.js", StringComparison.Ordinal);
        var tagStart = AppRazor.LastIndexOf('<', scriptTagStart);
        var tagEnd = AppRazor.IndexOf('>', scriptTagStart);
        var scriptTag = AppRazor[tagStart..(tagEnd + 1)];

        scriptTag.Should().NotContain("defer");
        scriptTag.Should().NotContain("async");
        scriptTag.Should().NotContain("type=\"module\"");
    }

    private static string AppRazorPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExcelETL.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the repository root (ExcelETL.slnx).");
        }

        return Path.Combine(directory.FullName, "src", "ExcelETL.BlazorAdmin", "Components", "App.razor");
    }
}
