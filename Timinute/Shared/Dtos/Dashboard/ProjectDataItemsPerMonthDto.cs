namespace Timinute.Shared.Dtos.Dashboard
{
    public class ProjectDataItemsPerMonthDto
    {
        public DateTimeOffset Time { get; set; }

        public IList<ProjectDataItemDto> ProjectDataItems { get; set; }
    }
}
