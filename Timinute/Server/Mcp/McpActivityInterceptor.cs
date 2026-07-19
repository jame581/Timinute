using System.Linq;
using ModelContextProtocol.Protocol;
using Timinute.Server.Models;

namespace Timinute.Server.Mcp
{
    /// <summary>
    /// Central call-tool interceptor (Task 8). Wraps every <c>tools/call</c> to:
    /// <list type="number">
    /// <item>enforce the <b>authoritative</b> write-scope gate (R3) — a read-only token calling a
    /// declared write tool is refused before the tool body runs;</item>
    /// <item>record exactly one <see cref="McpActivityLog"/> row (Success or Failed);</item>
    /// <item>emit one correlated Serilog event (CorrelationId comes from LogContext middleware).</item>
    /// </list>
    /// <para>
    /// Registered scoped and resolved from <c>request.Services</c> inside the filter delegate
    /// (never constructor-injected into the filter, which is a plain delegate).
    /// </para>
    /// </summary>
    public sealed class McpActivityInterceptor
    {
        // R3: the authoritative write-tool set. A future write tool is gated the moment its
        // wire name is added here, even if the tool forgets its per-tool RequireWrite() guard
        // (which stays as defense-in-depth).
        private static readonly HashSet<string> WriteTools = new(StringComparer.Ordinal)
        {
            "create_project", "log_time", "update_time_entry", "delete_time_entry"
        };

        private const string ReadOnlyMessage = "This token is read-only.";

        private readonly McpActivitySink sink;
        private readonly McpUserContext user;
        private readonly ILogger<McpActivityInterceptor> logger;

        public McpActivityInterceptor(McpActivitySink sink, McpUserContext user, ILogger<McpActivityInterceptor> logger)
        {
            this.sink = sink;
            this.user = user;
            this.logger = logger;
        }

        public async ValueTask<CallToolResult> RunAsync(string toolName, Func<ValueTask<CallToolResult>> next)
        {
            // R3 — authoritative write-scope gate. Short-circuit with a *clean* IsError result
            // rather than throwing: this filter runs inside the SDK's BuildInitialCallToolFilter,
            // whose catch would otherwise wrap any thrown exception as
            // "An error occurred invoking '<tool>'...". Returning a CallToolResult bypasses that,
            // so the client sees exactly "This token is read-only.".
            if (WriteTools.Contains(toolName) && !user.CanWrite)
            {
                await RecordAsync(toolName, $"{toolName} denied (read-only token)",
                    McpActivityResult.Failed, ReadOnlyMessage);
                logger.LogInformation(
                    "MCP tool {Tool} denied for read-only token, user {UserId}", toolName, user.UserId);
                return new CallToolResult
                {
                    IsError = true,
                    Content = [new TextContentBlock { Text = ReadOnlyMessage }]
                };
            }

            try
            {
                var result = await next();

                // A tool may either throw (handled below) or *return* an IsError result. Both are
                // failures for the audit trail — record Failed in either case so a domain error
                // (e.g. "Time entry not found.") is never logged as Success.
                if (result.IsError == true)
                {
                    await RecordAsync(toolName, $"{toolName} failed", McpActivityResult.Failed, ExtractText(result));
                    logger.LogError("MCP tool {Tool} returned an error result for user {UserId}", toolName, user.UserId);
                }
                else
                {
                    await RecordAsync(toolName, $"{toolName} ok", McpActivityResult.Success, null);
                    logger.LogInformation("MCP tool {Tool} succeeded for user {UserId}", toolName, user.UserId);
                }

                return result;
            }
            catch (Exception ex)
            {
                await RecordAsync(toolName, $"{toolName} failed", McpActivityResult.Failed, ex.Message);
                logger.LogError(ex, "MCP tool {Tool} failed for user {UserId}", toolName, user.UserId);
                throw;
            }
        }

        // Best-effort audit write: the audit trail must never mask or replace the tool's own
        // outcome, and a failed audit write must not turn a successful tool call into a failure.
        // On error we log via ILogger and swallow, so the caller's result/exception is preserved.
        // Uses CancellationToken.None deliberately: a client that cancels/disconnects must still
        // leave exactly one audit row — the record must not be tied to the request's lifetime.
        private async Task RecordAsync(string toolName, string summary, McpActivityResult result, string? detail)
        {
            try
            {
                await sink.WriteAsync(user.UserId, user.TokenId, toolName, summary, result, detail, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write MCP activity row for tool {Tool}", toolName);
            }
        }

        private static string? ExtractText(CallToolResult result) =>
            result.Content?.OfType<TextContentBlock>().FirstOrDefault()?.Text;
    }
}
