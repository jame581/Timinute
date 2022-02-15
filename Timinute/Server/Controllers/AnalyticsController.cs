using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Shared.Dtos.Dashboard;

namespace Timinute.Server.Controllers
{
    [Authorize]
    [ApiController]
    [ResponseCache(CacheProfileName = "Default120")]
    [Route("[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IRepository<Project> projectRepository;
        private readonly IRepository<TrackedTask> trackedTaskRepository;
        private readonly IMapper mapper;
        private readonly ILogger<AnalyticsController> logger;

        public AnalyticsController(IRepositoryFactory repositoryFactory, IMapper mapper, ILogger<AnalyticsController> logger)
        {
            this.mapper = mapper;
            this.logger = logger;

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
                projectDataItemsPerMonth.Time = new DateTime(projectTimeByMonth.Key.Year, projectTimeByMonth.Key.Month, 1);
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
                workTimePerMonth.Time = new DateTime(projectTimeByMonth.Key.Year, projectTimeByMonth.Key.Month, 1).ToString("yyyy MMM");
                workTimePerMonth.WorkTimeInSeconds = projectTimeByMonth.Sum(x => x.Duration.TotalSeconds);
                workTimePerMonthsDto.Add(workTimePerMonth);
            }

            return Ok(workTimePerMonthsDto);
        }

        // GET: api/Analytics/AmountWorkTimeLastMonth
        [HttpGet("AmountWorkTimeLastMonth")]
        public async Task<ActionResult<AmountOfWorkTimeDto>> GetAmountWorkTimeLastMonth()
        {
            // get current user ID
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var today = DateTime.Today;
            var month = new DateTime(today.Year, today.Month, 1);
            var first = month.AddMonths(-1);
            var last = month.AddDays(-1);

            var trackedTaskList = await trackedTaskRepository.Get(
                x => x.UserId == userId && x.StartDate >= first && x.StartDate <= last,
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
