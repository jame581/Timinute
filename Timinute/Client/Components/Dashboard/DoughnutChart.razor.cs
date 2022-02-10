using Timinute.Client.Models.Dashboard;
namespace Timinute.Client.Components.Dashboard
{
    public partial class DoughnutChart
    {
        ProjectDataItem[] projectTime = new ProjectDataItem[] {
            new ProjectDataItem("Project 1", TimeSpan.FromMinutes(60)),
            new ProjectDataItem("Project 2", TimeSpan.FromMinutes(120)),
            new ProjectDataItem("Project 3", TimeSpan.FromMinutes(240)),
            new ProjectDataItem("Project 4", TimeSpan.FromMinutes(275)),
            new ProjectDataItem("Project 5", TimeSpan.FromMinutes(150)),
        };
    }
}
