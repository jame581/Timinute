namespace Timinute.Server.Models
{
    /// <summary>
    /// The caller's in-progress stopwatch. At most one per user (PK = UserId).
    /// Deliberately not a <see cref="TrackedTask"/>: a running timer must never show up in
    /// analytics, exports, search or MCP results. Stopping it creates the TrackedTask.
    /// ProjectId has no FK to Project on purpose — a project deleted mid-run must not block the stop.
    /// </summary>
    public class RunningTimer
    {
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? ProjectId { get; set; }
        public DateTimeOffset StartDate { get; set; }
    }
}
