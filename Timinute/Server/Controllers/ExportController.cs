using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Models.Export;
using Timinute.Server.Repository;
using Timinute.Server.Services;

namespace Timinute.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class ExportController : ControllerBase
    {
        private readonly IRepository<TrackedTask> taskRepository;
        private readonly IExportService exportService;
        private readonly ILogger<ExportController> logger;

        public ExportController(IRepositoryFactory repositoryFactory, IExportService exportService, ILogger<ExportController> logger)
        {
            this.exportService = exportService;
            this.logger = logger;

            taskRepository = repositoryFactory.GetRepository<TrackedTask>();
        }

        [HttpGet("tasks")]
        public async Task<ActionResult> ExportTasks(
            [FromQuery] string? format = "csv",
            [FromQuery] DateTimeOffset? from = null,
            [FromQuery] DateTimeOffset? to = null,
            [FromQuery] string? projectId = null,
            [FromQuery] string? search = null)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var tasks = await taskRepository.Get(
                t => t.UserId == userId
                    && (from == null || t.StartDate >= from.Value.ToUniversalTime())
                    && (to == null || t.StartDate <= to.Value.ToUniversalTime())
                    && (projectId == null || t.ProjectId == projectId)
                    && (search == null || t.Name.Contains(search, StringComparison.OrdinalIgnoreCase)),
                orderBy: t => t.OrderByDescending(x => x.StartDate),
                includeProperties: nameof(Project));

            var exportData = tasks.Select(t => new TaskExportDto
            {
                Name = SanitizeForExport(t.Name),
                ProjectName = SanitizeForExport(t.Project?.Name ?? "None"),
                StartDate = t.StartDate.UtcDateTime.ToString("yyyy-MM-dd HH:mm"),
                EndDate = t.EndDate?.UtcDateTime.ToString("yyyy-MM-dd HH:mm") ?? "",
                Duration = FormatDuration(t.Duration),
                Date = t.StartDate.UtcDateTime.ToString("yyyy-MM-dd"),
            }).ToList();

            var resolvedFormat = ResolveFormat(format);
            logger.LogInformation("User exported {Count} tracked tasks as {Format}", exportData.Count, resolvedFormat);
            return GenerateFile(exportData, resolvedFormat, "tracked-tasks");
        }

        [HttpGet("projects")]
        public async Task<ActionResult> ExportProjects(
            [FromQuery] string? format = "csv",
            [FromQuery] DateTimeOffset? from = null,
            [FromQuery] DateTimeOffset? to = null,
            [FromQuery] string? search = null)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var tasks = await taskRepository.Get(
                t => t.UserId == userId
                    && (from == null || t.StartDate >= from.Value.ToUniversalTime())
                    && (to == null || t.StartDate <= to.Value.ToUniversalTime()),
                includeProperties: nameof(Project));

            var groups = tasks.GroupBy(t => t.ProjectId ?? "None");

            if (!string.IsNullOrEmpty(search))
            {
                groups = groups.Where(g => (g.First().Project?.Name ?? "None").Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var exportData = groups.Select(g => new ProjectExportDto
            {
                ProjectName = SanitizeForExport(g.First().Project?.Name ?? "None"),
                TotalHours = FormatDuration(TimeSpan.FromSeconds(g.Sum(t => t.Duration.TotalSeconds))),
                TaskCount = g.Count(),
            }).ToList();

            var resolvedFormat = ResolveFormat(format);
            logger.LogInformation("User exported {Count} project summaries as {Format}", exportData.Count, resolvedFormat);
            return GenerateFile(exportData, resolvedFormat, "projects");
        }

        [HttpGet("analytics")]
        public async Task<ActionResult> ExportAnalytics(
            [FromQuery] string? format = "csv",
            [FromQuery] DateTimeOffset? from = null,
            [FromQuery] DateTimeOffset? to = null)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var tasks = await taskRepository.Get(
                t => t.UserId == userId
                    && (from == null || t.StartDate >= from.Value.ToUniversalTime())
                    && (to == null || t.StartDate <= to.Value.ToUniversalTime()),
                includeProperties: nameof(Project));

            var exportData = tasks
                .GroupBy(t => new { t.StartDate.Year, t.StartDate.Month })
                .Select(g =>
                {
                    var topProject = g
                        .GroupBy(t => t.ProjectId)
                        .OrderByDescending(pg => pg.Sum(t => t.Duration.TotalSeconds))
                        .First();

                    var topProjectName = topProject.First().Project?.Name ?? "None";
                    var topProjectSeconds = topProject.Sum(t => t.Duration.TotalSeconds);

                    return new AnalyticsExportDto
                    {
                        Month = new DateTimeOffset(g.Key.Year, g.Key.Month, 1, 0, 0, 0, TimeSpan.Zero).ToString("yyyy MMM"),
                        TotalHours = FormatDuration(TimeSpan.FromSeconds(g.Sum(t => t.Duration.TotalSeconds))),
                        TopProject = SanitizeForExport(topProjectName),
                        TopProjectHours = FormatDuration(TimeSpan.FromSeconds(topProjectSeconds)),
                    };
                })
                .ToList();

            var resolvedFormat = ResolveFormat(format);
            logger.LogInformation("User exported {Count} monthly analytics as {Format}", exportData.Count, resolvedFormat);
            return GenerateFile(exportData, resolvedFormat, "analytics");
        }

        private ActionResult GenerateFile<T>(List<T> data, string resolvedFormat, string filePrefix)
        {
            var date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

            if (resolvedFormat == "xlsx")
            {
                var bytes = exportService.ToExcel(data, filePrefix);
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{filePrefix}-export-{date}.xlsx");
            }

            var csvBytes = exportService.ToCsv(data);
            return File(csvBytes, "text/csv", $"{filePrefix}-export-{date}.csv");
        }

        private static string ResolveFormat(string? format)
        {
            var normalized = format?.ToLowerInvariant();
            return normalized == "xlsx" ? "xlsx" : "csv";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            int hours = (duration.Days * 24) + duration.Hours;
            return $"{hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }

        private static string SanitizeForExport(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            foreach (var c in value)
            {
                if (char.IsWhiteSpace(c))
                    continue;

                if (c is '=' or '+' or '-' or '@')
                    return "'" + value;

                break;
            }

            return value;
        }
    }
}
