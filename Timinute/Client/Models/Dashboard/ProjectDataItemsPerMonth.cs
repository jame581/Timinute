namespace Timinute.Client.Models.Dashboard
{
    public class ProjectDataItemsPerMonth
    {
        public DateTime Time { get; set; }

        public string TimeString { get => Time.ToString("yyyy MMMM"); }

        public IList<ProjectDataItem> ProjectDataItems { get; set; }

        public double ProjectDataTime { get => ProjectDataItems.Sum(x => x.TimeInt); }
    }
}
