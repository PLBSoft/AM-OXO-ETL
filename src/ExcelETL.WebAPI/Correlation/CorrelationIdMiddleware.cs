using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace ExcelETL.WebAPI.Correlation;

// A named middleware class, not an inline app.Use(...) lambda, specifically because of the
// execution-context-capture ordering constraint documented on its own registration in Program.cs
// (must run before UseRequestLocalization/UseExceptionHandler) -- giving it a discoverable,
// independently-testable home matches how every other order-sensitive pipeline concern in this
// host (ApiKeyAuthenticationHandler, GlobalExceptionHandler) is structured, rather than leaving a
// fragile ordering requirement attached only to a comment on an anonymous lambda.
//
// Client-supplied (X-Correlation-Id request header) if present, generated otherwise -- so no
// request is ever untraceable. Pushed into Serilog's LogContext (AsyncLocal-based) so every log
// line emitted anywhere during this request carries it automatically. Echoed back via
// Response.OnStarting rather than set directly, since ExceptionHandlerMiddleware clears the
// response (headers included) before invoking its handler on an unhandled exception; OnStarting
// callbacks fire only once the response is actually about to be written, after that clearing has
// already happened, so the header survives regardless of which path (success or error) produces
// the eventual response.
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdDefaults.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(CorrelationIdDefaults.LogPropertyName, correlationId))
        {
            await next(context);
        }
    }

    internal static string ResolveCorrelationId(HttpContext context) =>
        context.Request.Headers.TryGetValue(CorrelationIdDefaults.HeaderName, out var provided)
            && !string.IsNullOrWhiteSpace(provided)
            ? provided.ToString()
            : Guid.NewGuid().ToString();
}
