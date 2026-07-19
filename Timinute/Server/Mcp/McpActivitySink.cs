using Microsoft.EntityFrameworkCore;
using Timinute.Server.Data;
using Timinute.Server.Models;

namespace Timinute.Server.Mcp
{
    /// <summary>
    /// Thin writer that persists one <see cref="McpActivityLog"/> row per tool call.
    /// <para>
    /// R4: uses a <b>separate</b> <see cref="ApplicationDbContext"/> created per write via
    /// <see cref="IDbContextFactory{TContext}"/> — never the request-scoped context. When a
    /// tool's own <c>DbUpdateException</c> poisons the request-scoped context, that context can
    /// no longer <c>SaveChanges</c>; a fresh factory-created context still records the Failed
    /// audit row.
    /// </para>
    /// </summary>
    public sealed class McpActivitySink
    {
        private readonly IDbContextFactory<ApplicationDbContext> dbFactory;

        public McpActivitySink(IDbContextFactory<ApplicationDbContext> dbFactory) => this.dbFactory = dbFactory;

        public async Task WriteAsync(string userId, string? tokenId, string tool, string summary,
            McpActivityResult result, string? detail, CancellationToken ct)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            db.McpActivityLogs.Add(new McpActivityLog
            {
                UserId = userId,
                TokenId = tokenId,
                Tool = Truncate(tool, 64),
                Summary = Truncate(summary, 512),
                Result = result,
                Detail = detail is null ? null : Truncate(detail, 512),
                Timestamp = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);
    }
}
