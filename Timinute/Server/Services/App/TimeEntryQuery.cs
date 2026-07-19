namespace Timinute.Server.Services.App
{
    /// <summary>
    /// Filter inputs for <see cref="ITimeEntryAppService.SearchAsync"/>. Mirrors the
    /// query parameters of <c>TrackedTaskController.SearchTrackedTasks</c>; normalization
    /// (whitespace→null, trim, tag-id dedupe) happens inside the shared predicate builder.
    /// </summary>
    public class TimeEntryQuery
    {
        public DateTimeOffset? From { get; set; }
        public DateTimeOffset? To { get; set; }
        public string? ProjectId { get; set; }
        public string? Search { get; set; }
        public List<string>? TagIds { get; set; }
    }
}
