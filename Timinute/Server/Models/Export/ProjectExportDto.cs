namespace Timinute.Server.Models.Export
{
    public class ProjectExportDto
    {
        public string ProjectName { get; set; } = null!;
        public string TotalHours { get; set; } = null!;
        public int TaskCount { get; set; }
    }
}
