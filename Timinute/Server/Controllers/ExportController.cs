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
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
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
                    && (search == null || t.Name.Contains(search)),
                includeProperties: nameof(Project));

            var exportData = tasks.Select(t => new TaskExportDto
            {
                Name = t.Name,
                ProjectName = t.Project?.Name ?? "None",
                StartDate = t.StartDate.ToString("yyyy-MM-dd HH:mm"),
                EndDate = t.EndDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
                Duration = FormatDuration(t.Duration),
                Date = t.StartDate.ToString("yyyy-MM-dd"),
            }).ToList();

            logger.LogInformation("User {UserId} exported {Count} tracked tasks as {Format}", userId, exportData.Count, format);
            return GenerateFile(exportData, format, "tracked-tasks");
        }

        [HttpGet("projects")]
        public async Task<ActionResult> ExportProjects(
            [FromQuery] string? format = "csv",
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
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

            var exportData = tasks
                .GroupBy(t => t.ProjectId ?? "None")
                .Select(g =>
                {
                    var projectName = g.First().Project?.Name ?? "None";
                    if (!string.IsNullOrEmpty(search) && !projectName.Contains(search, StringComparison.OrdinalIgnoreCase))
                        return null;

                    return new ProjectExportDto
                    {
                        ProjectName = projectName,
                        TotalHours = FormatDuration(TimeSpan.FromSeconds(g.Sum(t => t.Duration.TotalSeconds))),
                        TaskCount = g.Count(),
                    };
                })
                .Where(x => x != null)
                .ToList();

            logger.LogInformation("User {UserId} exported {Count} project summaries as {Format}", userId, exportData!.Count, format);
            return GenerateFile(exportData, format, "projects");
        }

        [HttpGet("analytics")]
        public async Task<ActionResult> ExportAnalytics(
            [FromQuery] string? format = "csv",
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
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
                        Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("yyyy MMM"),
                        TotalHours = FormatDuration(TimeSpan.FromSeconds(g.Sum(t => t.Duration.TotalSeconds))),
                        TopProject = topProjectName,
                        TopProjectHours = FormatDuration(TimeSpan.FromSeconds(topProjectSeconds)),
                    };
                })
                .ToList();

            logger.LogInformation("User {UserId} exported {Count} monthly analytics as {Format}", userId, exportData.Count, format);
            return GenerateFile(exportData, format, "analytics");
        }

        private ActionResult GenerateFile<T>(List<T> data, string? format, string filePrefix)
        {
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");

            if (format?.ToLowerInvariant() == "xlsx")
            {
                var bytes = exportService.ToExcel(data, filePrefix);
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{filePrefix}-export-{date}.xlsx");
            }

            var csvBytes = exportService.ToCsv(data);
            return File(csvBytes, "text/csv", $"{filePrefix}-export-{date}.csv");
        }

        private static string FormatDuration(TimeSpan duration)
        {
            int hours = (duration.Days * 24) + duration.Hours;
            return $"{hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }
    }
}
