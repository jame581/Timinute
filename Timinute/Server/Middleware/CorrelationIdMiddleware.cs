using System.Text.RegularExpressions;
using Serilog.Context;

namespace Timinute.Server.Middleware
{
    // Assigns a CorrelationId per request so every log line within the request
    // is correlatable. Reuses a safe inbound X-Correlation-Id, otherwise generates
    // one. The value is echoed on the response header.
    public sealed partial class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-Id";
        private const string LogPropertyName = "CorrelationId";
        private readonly RequestDelegate next;

        public CorrelationIdMiddleware(RequestDelegate next) => this.next = next;

        public async Task Invoke(HttpContext context)
        {
            var correlationId = ResolveCorrelationId(context);

            // Apply via OnStarting (fires just before headers are sent) rather than
            // setting the header directly, so the value survives Response.Clear()
            // called by UseExceptionHandler when re-executing to the error page.
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = correlationId;
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty(LogPropertyName, correlationId))
            {
                await next(context);
            }
        }

        // Only accept an inbound id that is short and URL-safe, to avoid header/log
        // injection; otherwise generate a fresh one.
        private static string ResolveCorrelationId(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out var value))
            {
                var candidate = value.ToString();
                if (!string.IsNullOrWhiteSpace(candidate)
                    && candidate.Length <= 64
                    && SafeIdRegex().IsMatch(candidate))
                {
                    return candidate;
                }
            }

            return Guid.NewGuid().ToString("n");
        }

        [GeneratedRegex("^[A-Za-z0-9._-]+$")]
        private static partial Regex SafeIdRegex();
    }
}
