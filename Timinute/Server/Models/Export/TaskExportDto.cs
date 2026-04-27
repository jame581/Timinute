namespace Timinute.Server.Models.Export
{
    public class TaskExportDto
    {
        public string Name { get; set; } = null!;
        public string ProjectName { get; set; } = null!;
        public string StartDate { get; set; } = null!;
        public string EndDate { get; set; } = null!;
        public string Duration { get; set; } = null!;
        public string Date { get; set; } = null!;
    }
}
