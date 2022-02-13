namespace Timinute.Shared.Dtos.Dashboard
{
    public class ProjectDataItemsPerMonthDto
    {
        public DateTime Time { get; set; }

        public IList<ProjectDataItemDto> ProjectDataItems { get; set; }
    }
}
