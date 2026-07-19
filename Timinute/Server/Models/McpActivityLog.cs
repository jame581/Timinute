namespace Timinute.Server.Models
{
    public enum McpActivityResult { Success, Failed }

    /// <summary>
    /// One audit row per MCP tool call (read and write), written by the central
    /// <c>McpActivityInterceptor</c>. Retained 90 days (purge is Task 10).
    /// </summary>
    public class McpActivityLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = null!;
        public DateTimeOffset Timestamp { get; set; }
        public string Tool { get; set; } = null!;
        public string Summary { get; set; } = "";
        public McpActivityResult Result { get; set; }
        public string? Detail { get; set; }
        public string? TokenId { get; set; }
    }
}
