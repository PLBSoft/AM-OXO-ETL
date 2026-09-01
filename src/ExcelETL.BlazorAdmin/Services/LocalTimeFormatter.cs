using Microsoft.JSInterop;

namespace ExcelETL.BlazorAdmin.Services;

public sealed class LocalTimeFormatter(IJSRuntime jsRuntime) : ILocalTimeFormatter
{
    public Task<string> FormatAsync(DateTime utcValue, string pattern) =>
        jsRuntime.InvokeAsync<string>("amOxoLocalTime.format", ToIso8601Utc(utcValue), pattern).AsTask();

    public async Task<IReadOnlyList<string>> FormatManyAsync(IReadOnlyList<DateTime> utcValues, string pattern)
    {
        var isoValues = utcValues.Select(ToIso8601Utc).ToArray();
        var formatted = await jsRuntime.InvokeAsync<string[]>("amOxoLocalTime.formatMany", isoValues, pattern);
        return formatted;
    }

    // EF Core/plain-DateTime values arriving here are typically DateTimeKind.Unspecified even
    // though they are already known to hold UTC -- "o" only appends the "Z" suffix (required for
    // the browser's `new Date(...)` to interpret the string as UTC rather than local time) when
    // Kind is explicitly Utc, so it's forced here rather than trusted from the source value.
    private static string ToIso8601Utc(DateTime utcValue) =>
        DateTime.SpecifyKind(utcValue, DateTimeKind.Utc).ToString("o");
}
