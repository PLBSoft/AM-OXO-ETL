// Lot 064: converts a UTC timestamp (always passed in as a genuine ISO 8601 UTC string, i.e.
// ending in "Z" -- see LocalTimeFormatter.cs, which forces DateTimeKind.Utc before serializing)
// into the browser's own local time, using the time zone the browser already knows about --
// never configured, never sent up to the server. Plain global script (not a Blazor JS module),
// same convention as theme.js: invoked via IJSRuntime from each page's own OnAfterRenderAsync,
// after the interactive circuit is up (JS interop isn't available during prerendering).
window.amOxoLocalTime = (function () {
    function partsOf(isoUtc) {
        var date = new Date(isoUtc);
        var formatter = new Intl.DateTimeFormat(undefined, {
            year: "numeric", month: "2-digit", day: "2-digit",
            hour: "2-digit", minute: "2-digit", second: "2-digit",
            hourCycle: "h23"
        });
        var parts = {};
        formatter.formatToParts(date).forEach(function (part) {
            parts[part.type] = part.value;
        });
        return parts;
    }

    // `pattern` supports only the literal tokens this app's own date-formatting call sites ever
    // use (yyyy, MM, dd, HH, mm, ss) -- not a general .NET/ICU pattern parser, deliberately kept
    // this narrow since every caller already knows its own fixed pattern.
    function format(isoUtc, pattern) {
        var parts = partsOf(isoUtc);
        return pattern
            .replace("yyyy", parts.year)
            .replace("MM", parts.month)
            .replace("dd", parts.day)
            .replace("HH", parts.hour)
            .replace("mm", parts.minute)
            .replace("ss", parts.second);
    }

    function formatMany(isoUtcValues, pattern) {
        return isoUtcValues.map(function (isoUtc) {
            return format(isoUtc, pattern);
        });
    }

    return { format: format, formatMany: formatMany };
})();
