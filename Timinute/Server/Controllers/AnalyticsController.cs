using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Timinute.Server.Data;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Server.Services.App;
using Timinute.Shared.Dtos.Analytics;
using Timinute.Shared.Dtos.Dashboard;

namespace Timinute.Server.Controllers
{
    [Authorize]
    [ApiController]
    [ResponseCache(CacheProfileName = Constants.CacheProfiles.Default120)]
    [Route("[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IRepository<Project> projectRepository;
        private readonly IRepository<TrackedTask> trackedTaskRepository;
        private readonly IMapper mapper;
        private readonly ILogger<AnalyticsController> logger;
        private readonly ApplicationDbContext dbContext;
        private readonly IAnalyticsAppService analyticsAppService;

        public AnalyticsController(IRepositoryFactory repositoryFactory, IMapper mapper, ILogger<AnalyticsController> logger, ApplicationDbContext dbContext, IAnalyticsAppService analyticsAppService)
        {
            this.mapper = mapper;
            this.logger = logger;
            this.dbContext = dbContext;
            this.analyticsAppService = analyticsAppService;

            projectRepository = repositoryFactory.GetRepository<Project>();
            trackedTaskRepository = repositoryFactory.GetRepository<TrackedTask>();
        }

        // GET: api/Analytics/ProjectWorkTime
        [HttpGet("ProjectWorkTime")]
        public async Task<ActionResult<IEnumerable<ProjectDataItemDto>>> GetProjectWorkTime()
        {
            // get current user ID
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // TODO(jame_581): Use Dapper for this Queries and let compute time on DB
            var trackedTaskList = await trackedTaskRepository.Get(x => x.UserId == userId, includeProperties: nameof(Project));

            var projectTimes = trackedTaskList.GroupBy(t => t.ProjectId, (key, t) =>
            {
                var array = t as TrackedTask[] ?? t.ToArray();

                string projectName = "None";
                if (array.Length > 0)
                {
                    projectName = array[0].Project == null ? "None" : array[0].Project.Name;
                }

                return new
                {
                    ProjectId = key == null ? "None" : key,
                    ProjectName = projectName,
                    ProjectTimeInSeconds = array.Sum(x => x.Duration.TotalSeconds),
                    Count = array.Length
                };
            }).ToList();

            var projectLiset = new List<ProjectDataItemDto>();
            foreach (var projectTime in projectTimes)
            {
                projectLiset.Add(new ProjectDataItemDto
                {
                    ProjectId = projectTime.ProjectId,
                    ProjectName = projectTime.ProjectName,
                    Time = TimeSpan.FromSeconds(projectTime.ProjectTimeInSeconds)
                });
            }
            projectLiset = projectLiset.OrderByDescending(x => x.Time).ToList();
            return Ok(projectLiset);
        }

        // GET: api/Analytics/ProjectWorkTimePerMonths
        [HttpGet("ProjectWorkTimePerMonths")]
        public async Task<ActionResult<IEnumerable<ProjectDataItemsPerMonthDto>>> GetProjectWorkTimePerMonths()
        {
            // get current user ID
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // TODO(jame_581): Use Dapper for this Queries and let compute time on DB
            var trackedTaskList = await trackedTaskRepository.Get(x => x.UserId == userId, includeProperties: nameof(Project));

            var groupedByDate = trackedTaskList
                .AsParallel()
                .GroupBy(x => new { x.StartDate.Year, x.StartDate.Month })
                .ToList();

            var projectLiset = new List<ProjectDataItemsPerMonthDto>();
            foreach (var projectTimeByMonth in groupedByDate)
            {
                var projectDataItemsPerMonth = new ProjectDataItemsPerMonthDto();
                projectDataItemsPerMonth.Time = new DateTimeOffset(projectTimeByMonth.Key.Year, projectTimeByMonth.Key.Month, 1, 0, 0, 0, TimeSpan.Zero);
                projectDataItemsPerMonth.ProjectDataItems = new List<ProjectDataItemDto>();

                foreach (var item in projectTimeByMonth)
                {
                    projectDataItemsPerMonth.ProjectDataItems.Add(
                        new ProjectDataItemDto
                        {
                            ProjectId = item.ProjectId == null ? "None" : item.ProjectId,
                            ProjectName = item.Project == null ? "None" : item.Project.Name,
                            Time = item.Duration
                        }
                    );
                }

                projectLiset.Add(projectDataItemsPerMonth);
            }

            return Ok(projectLiset);
        }

        // GET: api/Analytics/WorkTimePerMonths
        [HttpGet("WorkTimePerMonths")]
        public async Task<ActionResult<IEnumerable<WorkTimePerMonthDto>>> GetWorkTimePerMonths()
        {
            // get current user ID
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // TODO(jame_581): Use Dapper for this Queries and let compute time on DB
            var trackedTaskList = await trackedTaskRepository.Get(x => x.UserId == userId, includeProperties: nameof(Project));

            var groupedByDate = trackedTaskList
                .AsParallel()
                .GroupBy(x => new { x.StartDate.Year, x.StartDate.Month })
                .ToList();

            var workTimePerMonthsDto = new List<WorkTimePerMonthDto>();
            foreach (var projectTimeByMonth in groupedByDate)
            {
                var workTimePerMonth = new WorkTimePerMonthDto();
                workTimePerMonth.Time = new DateTimeOffset(projectTimeByMonth.Key.Year, projectTimeByMonth.Key.Month, 1, 0, 0, 0, TimeSpan.Zero).ToString("yyyy MMM");
                workTimePerMonth.WorkTimeInSeconds = projectTimeByMonth.Sum(x => x.Duration.TotalSeconds);
                workTimePerMonthsDto.Add(workTimePerMonth);
            }

            return Ok(workTimePerMonthsDto);
        }

        // GET: api/Analytics/AmountWorkTimeLastMonth
        [HttpGet("AmountWorkTimeByMonth")]
        public async Task<ActionResult<AmountOfWorkTimeDto>> GetAmountWorkTimeByMonth([FromQuery] AmountWorkTimeByMonthDto amountWorkTimeByMonthDto)
        {
            // get current user ID
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var month = new DateTimeOffset(amountWorkTimeByMonthDto.Year, amountWorkTimeByMonthDto.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var last = month.AddMonths(1).AddDays(-1);

            var trackedTaskList = await trackedTaskRepository.Get(
                x => x.UserId == userId && x.StartDate >= month && x.StartDate <= last,
                includeProperties: "Project");

            double secondsSum = trackedTaskList.ToList().Sum(x => x.Duration.TotalSeconds);

            string amountWorkTimeLastMonthText = GetAmountWorkTimeFormatted(secondsSum);

            var (projectName, timeInSeconds) = FindTopProjectLastMonth(trackedTaskList);

            var amountWorkTimeLastMonth = new AmountOfWorkTimeDto
            {
                AmountWorkTime = secondsSum,
                AmountWorkTimeText = amountWorkTimeLastMonthText,
                TopProject = projectName,
                TopProjectAmounTime = timeInSeconds,
                TopProjectAmounTimeText = GetAmountWorkTimeFormatted(timeInSeconds),
            };

            return Ok(amountWorkTimeLastMonth);
        }

        // GET: Analytics/summary?From=...&To=...&TzOffsetMinutes=...
        [HttpGet("summary")]
        [ResponseCache(NoStore = true)]
        public async Task<ActionResult<AnalyticsSummaryDto>> GetRangeSummary([FromQuery] AnalyticsRangeDto range)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var summary = await analyticsAppService.SummaryAsync(userId, range.From, range.To, range.TzOffsetMinutes);
            return Ok(summary);
        }

        // GET: Analytics/daily
        [HttpGet("daily")]
        [ResponseCache(NoStore = true)]
        public async Task<ActionResult<IEnumerable<DailyAnalyticsDto>>> GetDailyBreakdown([FromQuery] AnalyticsRangeDto range)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var offset = TimeSpan.FromMinutes(range.TzOffsetMinutes);
            var rows = await RangeQuery(userId, range)
                .Select(t => new { t.StartDate, t.Duration })
                .ToListAsync();

            var days = rows
                .GroupBy(r => r.StartDate.ToOffset(offset).Date)
                .Select(g => new DailyAnalyticsDto
                {
                    Date = g.Key,
                    TotalDuration = TimeSpan.FromTicks(g.Sum(x => x.Duration.Ticks)),
                    TaskCount = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToList();

            return Ok(days);
        }

        // GET: Analytics/projects
        [HttpGet("projects")]
        [ResponseCache(NoStore = true)]
        public async Task<ActionResult<IEnumerable<ProjectAnalyticsDto>>> GetProjectBreakdown([FromQuery] AnalyticsRangeDto range)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Soft-deleted projects are filtered by the global query filter, so their
            // navigation resolves to null and those tasks land in the "_none" bucket.
            var rows = await RangeQuery(userId, range)
                .Select(t => new
                {
                    t.ProjectId,
                    ProjectName = t.Project != null ? t.Project.Name : null,
                    ProjectColor = t.Project != null ? t.Project.Color : null,
                    t.Duration
                })
                .ToListAsync();

            var projects = rows
                .GroupBy(r => r.ProjectName == null ? "_none" : r.ProjectId!)
                .Select(g => new ProjectAnalyticsDto
                {
                    ProjectId = g.Key,
                    Name = g.First().ProjectName ?? "No project",
                    Color = g.First().ProjectColor,
                    TotalDuration = TimeSpan.FromTicks(g.Sum(x => x.Duration.Ticks)),
                    TaskCount = g.Count()
                })
                .OrderByDescending(p => p.TotalDuration)
                .ToList();

            return Ok(projects);
        }

        // GET: Analytics/tags
        [HttpGet("tags")]
        [ResponseCache(NoStore = true)]
        public async Task<ActionResult<IEnumerable<TagAnalyticsDto>>> GetTagBreakdown([FromQuery] AnalyticsRangeDto range)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var rows = await RangeQuery(userId, range)
                .Select(t => new
                {
                    t.Duration,
                    Tags = t.Tags.Select(tag => new { tag.TagId, tag.Name, tag.Color }).ToList()
                })
                .ToListAsync();

            var buckets = new Dictionary<string, TagAnalyticsDto>();
            foreach (var row in rows)
            {
                if (row.Tags.Count == 0)
                {
                    Accumulate(buckets, "_untagged", "Untagged", null, row.Duration);
                    continue;
                }
                foreach (var tag in row.Tags)
                {
                    Accumulate(buckets, tag.TagId, tag.Name, tag.Color, row.Duration);
                }
            }

            return Ok(buckets.Values.OrderByDescending(t => t.TotalDuration).ToList());
        }

        private IQueryable<TrackedTask> RangeQuery(string userId, AnalyticsRangeDto range)
        {
            var fromUtc = range.From.ToUniversalTime();
            var toUtc = range.To.ToUniversalTime();

            return dbContext.TrackedTasks
                .AsNoTracking()
                .Where(t => t.UserId == userId && t.StartDate >= fromUtc && t.StartDate <= toUtc);
        }

        private static void Accumulate(Dictionary<string, TagAnalyticsDto> buckets, string key, string name, string? color, TimeSpan duration)
        {
            if (!buckets.TryGetValue(key, out var dto))
            {
                dto = new TagAnalyticsDto { TagId = key, Name = name, Color = color };
                buckets[key] = dto;
            }
            dto.TotalDuration += duration;
            dto.TaskCount++;
        }

        private (string, double) FindTopProjectLastMonth(IEnumerable<TrackedTask> trackedTasks)
        {
            string projectName = "None";
            double maxSeconds = 0;

            var groupedByProject = trackedTasks
                .AsParallel()
                .GroupBy(x => new { x.ProjectId })
                .ToList();

            foreach (var project in groupedByProject)
            {
                var seconds = project.Sum(x => x.Duration.TotalSeconds);

                if (seconds > maxSeconds)
                {
                    maxSeconds = seconds;
                    var topProject = project.FirstOrDefault();
                    if (topProject != null)
                    {
                        projectName = topProject.Project == null ? "None" : topProject.Project.Name;
                    }
                }
            }

            return (projectName, maxSeconds);
        }

        private static string GetAmountWorkTimeFormatted(double secondsSum)
        {
            TimeSpan totalTime = TimeSpan.FromSeconds(secondsSum);
            int hours = (totalTime.Days * 24) + totalTime.Hours;
            string amountWorkTimeLastMonthText = $"{hours}:{totalTime.Minutes:00}:{totalTime.Seconds:00}";
            return amountWorkTimeLastMonthText;
        }
    }
}
