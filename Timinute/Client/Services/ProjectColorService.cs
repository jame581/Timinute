using Timinute.Shared.Dtos.Project;

namespace Timinute.Client.Services
{
    public class ProjectColorService
    {
        private static readonly string[] Palette =
        {
            "#6366F1", "#F59E0B", "#10B981", "#EC4899", "#94A3B8"
        };

        public string GetColor(ProjectDto? project)
        {
            if (project == null) return Palette[4];
            if (!string.IsNullOrWhiteSpace(project.Color)) return project.Color!;
            return GetColorById(project.ProjectId);
        }

        public string GetColorById(string? projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId)) return Palette[4];
            // Mask the sign bit to avoid the int.MinValue overflow that Math.Abs cannot handle.
            var hash = projectId.GetHashCode() & int.MaxValue;
            return Palette[hash % Palette.Length];
        }

        public IReadOnlyList<string> GetPalette() => Palette;
    }
}
