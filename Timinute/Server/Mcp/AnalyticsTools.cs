using System.ComponentModel;
using ModelContextProtocol.Server;
using Timinute.Server.Services.App;

namespace Timinute.Server.Mcp
{
    /// <summary>
    /// MCP tools over the caller's analytics. Constructed per tool call inside the HTTP
    /// request's DI scope; <see cref="McpUserContext.UserId"/> is resolved per call, never
    /// cached in a field. Read-only surface.
    /// </summary>
    [McpServerToolType]
    public class AnalyticsTools
    {
        private readonly IAnalyticsAppService analytics;
        private readonly McpUserContext user;

        public AnalyticsTools(IAnalyticsAppService analytics, McpUserContext user)
        {
            this.analytics = analytics;
            this.user = user;
        }

        [McpServerTool(Name = "get_analytics_summary"), Description("Get a summary of the current user's tracked time over a date range (total, task count, active days, target).")]
        public async Task<object> GetAnalyticsSummary(
            [Description("Inclusive start of the range (include the offset).")] DateTimeOffset from,
            [Description("Inclusive end of the range (include the offset).")] DateTimeOffset to,
            [Description("The caller's timezone offset from UTC in minutes, used to bucket entries by local calendar day. Defaults to 0 (UTC).")] int tzOffsetMinutes = 0)
        {
            return await analytics.SummaryAsync(user.UserId, from, to, tzOffsetMinutes);
        }
    }
}
