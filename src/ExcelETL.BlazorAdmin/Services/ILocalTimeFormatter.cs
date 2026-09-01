namespace ExcelETL.BlazorAdmin.Services;

/// <summary>
/// Lot 064: converts a UTC <see cref="DateTime"/> into the browser's own local time, via JS
/// interop (<c>wwwroot/js/localTime.js</c>) -- never a server-side/appsettings time zone, per the
/// client's own explicit decision (see the lot 064 ticket). Only usable once the component's
/// circuit is interactive (JS interop throws during prerendering) -- callers gate on
/// <c>ComponentBase.RendererInfo.IsInteractive</c> themselves, same convention already used by
/// <c>PasswordChangeGuard.razor</c> (Lot 045).
/// </summary>
public interface ILocalTimeFormatter
{
    /// <summary>
    /// Formats a single UTC value. <paramref name="pattern"/> supports only the literal tokens
    /// yyyy/MM/dd/HH/mm/ss (see localTime.js) -- pass the same pattern the raw UTC value used to
    /// be rendered with, so the visual shape stays the same and only the time zone changes.
    /// </summary>
    Task<string> FormatAsync(DateTime utcValue, string pattern);

    /// <summary>
    /// Batched equivalent of <see cref="FormatAsync"/> -- one JS interop round trip for a whole
    /// list of values (e.g. every row of a table), rather than one call per row.
    /// </summary>
    Task<IReadOnlyList<string>> FormatManyAsync(IReadOnlyList<DateTime> utcValues, string pattern);
}
