namespace Timinute.Client.Models.Dashboard
{
    public class ProjectDataItem
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public TimeSpan Time { get; set; }
        public string YearAndMonth { get; set; }
        public double TimeInt { get => Time.TotalSeconds; }

        public ProjectDataItem(string projectId, string projectName, TimeSpan time, string yearAndMonth)
        {
            ProjectId = projectId;
            ProjectName = projectName;
            Time = time;
            YearAndMonth = yearAndMonth;
        }
    }
}
