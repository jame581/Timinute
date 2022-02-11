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

        // GET: api/Projects/ProjectWorkTime
        [HttpGet(Name = "ProjectWorkTime")]
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
                    ProjectTimeInSeconds = array.Sum(x => x.Duration.Seconds),
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

            return Ok(projectLiset);
        }
    }
}
