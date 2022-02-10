namespace Timinute.Client.Models.Dashboard
{
    public class ProjectDataItem
    {
        public string Project { get; set; }
        public TimeSpan Time { get; set; }
        public double TimeInt { get => Time.TotalSeconds; }

        public ProjectDataItem(string project, TimeSpan time)
        {
            Project = project;
            Time = time;
        }
    }
}
