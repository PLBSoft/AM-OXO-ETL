using Microsoft.AspNetCore.Localization;

namespace Microsoft.AspNetCore.Routing;

// An Interactive Server circuit is long-lived, so changing the active culture mid-circuit
// requires a real navigation, not a component re-render: this endpoint sets the standard
// culture cookie and redirects, which forces the next request (and the circuit it starts) to
// pick up the new RequestLocalizationOptions culture.
internal static class CultureEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapCultureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapGet("/culture/set", (string culture, string redirectUri, HttpContext httpContext) =>
        {
            httpContext.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

            return Results.LocalRedirect(redirectUri);
        });
    }
}
