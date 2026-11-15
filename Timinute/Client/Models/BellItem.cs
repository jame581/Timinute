namespace Timinute.Client.Models
{
    public enum BellRule
    {
        LongRunning,
        Idle,
    }

    /// <summary>One entry in the notification bell. Times are UTC (server clock); format with ToLocalTime().</summary>
    public sealed record BellItem(
        string Id,
        BellRule Rule,
        string Title,
        string Detail,
        DateTimeOffset TimerStart,
        DateTimeOffset? IdleStart = null,
        DateTimeOffset? IdleEnd = null);
}
