namespace Timinute.Shared.Dtos.Mcp
{
    public class McpActivityDto
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Tool { get; set; } = null!;
        public string Summary { get; set; } = "";
        public string Result { get; set; } = null!;
        public string? Detail { get; set; }
    }
}
