using Timinute.Client.Models.Dashboard;

namespace Timinute.Client.Components.Dashboard
{
    public partial class ProjectColumnChart
    {
        ProjectDataItem[] projectTimeMonth1 = new ProjectDataItem[] {
            new ProjectDataItem("ProjectId", "Project 1", TimeSpan.FromMinutes(60)),
            new ProjectDataItem("ProjectId", "Project 2", TimeSpan.FromMinutes(120)),
            new ProjectDataItem("ProjectId", "Project 3", TimeSpan.FromMinutes(240)),
            new ProjectDataItem("ProjectId", "Project 4", TimeSpan.FromMinutes(275)),
            new ProjectDataItem("ProjectId", "Project 5", TimeSpan.FromMinutes(150)),
        };

        ProjectDataItem[] projectTimeMonth2 = new ProjectDataItem[] {
            new ProjectDataItem("ProejctId", "Project 1", TimeSpan.FromMinutes(60)),
            new ProjectDataItem("ProejctId", "Project 2", TimeSpan.FromMinutes(120)),
            new ProjectDataItem("ProejctId", "Project 3", TimeSpan.FromMinutes(240)),
            new ProjectDataItem("ProejctId", "Project 4", TimeSpan.FromMinutes(275)),
            new ProjectDataItem("ProejctId", "Project 5", TimeSpan.FromMinutes(150)),
        };

        ProjectDataItem[] projectTimeMonth3 = new ProjectDataItem[] {
            new ProjectDataItem("ProejctId", "Project 1", TimeSpan.FromMinutes(60)),
            new ProjectDataItem("ProejctId", "Project 2", TimeSpan.FromMinutes(120)),
            new ProjectDataItem("ProejctId", "Project 3", TimeSpan.FromMinutes(240)),
            new ProjectDataItem("ProejctId", "Project 4", TimeSpan.FromMinutes(275)),
            new ProjectDataItem("ProejctId", "Project 5", TimeSpan.FromMinutes(150)),
        };

        string FormatTimeAsString(object value)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds((double)value);
            return timeSpan.ToString(@"hh\:mm\:ss");
        }
    }
}
