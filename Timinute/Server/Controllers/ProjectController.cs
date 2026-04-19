using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Models.Paging;
using Timinute.Server.Repository;
using Timinute.Shared.Dtos.Paging;
using Timinute.Shared.Dtos.Project;
using Timinute.Shared.Dtos.Trash;

namespace Timinute.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class ProjectController : ControllerBase
    {
        private readonly IRepository<Project> projectRepository;
        private readonly IRepository<TrackedTask> taskRepository;
        private readonly IMapper mapper;
        private readonly ILogger<ProjectController> logger;
        private readonly IConfiguration configuration;

        public ProjectController(IRepositoryFactory repositoryFactory, IMapper mapper, ILogger<ProjectController> logger, IConfiguration configuration)
        {
            this.mapper = mapper;
            this.logger = logger;
            this.configuration = configuration;

            projectRepository = repositoryFactory.GetRepository<Project>();
            taskRepository = repositoryFactory.GetRepository<TrackedTask>();
        }

        // GET: api/Projects
        [HttpGet(Name = "Projects")]
        public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjects([FromQuery] PagingParameters projectParameters)
        {
            // get current user ID
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var pagedProjectList = await projectRepository.GetPaged(projectParameters, project => project.UserId == userId);

            var metadata = new PaginationHeaderDto
            {
                TotalCount = pagedProjectList.TotalCount,
                PageSize = pagedProjectList.PageSize,
                CurrentPage = pagedProjectList.CurrentPage,
                TotalPages = pagedProjectList.TotalPages,
                HasNext = pagedProjectList.HasNext,
                HasPrevious = pagedProjectList.HasPrevious
            };

            Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(metadata));
            return Ok(mapper.Map<IEnumerable<ProjectDto>>(pagedProjectList));
        }

        // GET: api/Project/search
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<ProjectDto>>> SearchProjects(
            [FromQuery] PagingParameters pagingParameters,
            [FromQuery] string? search = null,
            [FromQuery] int? minTaskCount = null)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (minTaskCount.HasValue && minTaskCount.Value < 0)
            {
                return BadRequest("minTaskCount must be >= 0.");
            }

            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

            var pagedProjectList = await projectRepository.GetPaged(pagingParameters,
                p => p.UserId == userId
                    && (normalizedSearch == null || p.Name.Contains(normalizedSearch))
                    && (!minTaskCount.HasValue || p.TrackedTasks!.Count >= minTaskCount.Value),
                orderBy: nameof(Project.Name));

            var metadata = new PaginationHeaderDto
            {
                TotalCount = pagedProjectList.TotalCount,
                PageSize = pagedProjectList.PageSize,
                CurrentPage = pagedProjectList.CurrentPage,
                TotalPages = pagedProjectList.TotalPages,
                HasNext = pagedProjectList.HasNext,
                HasPrevious = pagedProjectList.HasPrevious
            };

            Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(metadata));
            return Ok(mapper.Map<IEnumerable<ProjectDto>>(pagedProjectList));
        }

        // GET: api/Project
        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectDto>> GetProject(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var project = await projectRepository.GetById(id);
            if (project == null || project.UserId != userId)
            {
                return NotFound("Project not found!");
            }
            return Ok(mapper.Map<ProjectDto>(project));
        }

        // CREATE: api/Project
        [HttpPost]
        public async Task<ActionResult<ProjectDto>> CreateProject([FromBody] CreateProjectDto project)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var newProject = mapper.Map<Project>(project);
            newProject.UserId = userId;

            await projectRepository.Insert(newProject);
            return Ok(mapper.Map<ProjectDto>(newProject));
        }

        // DELETE: api/Project
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteProject(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var projectToDelete = await projectRepository.GetById(id);
            if (projectToDelete == null)
            {
                logger.LogError("Project was not found");
                return NotFound("Project not found!");
            }

            if (projectToDelete.UserId != userId)
            {
                return NotFound("Project not found!");
            }

            var deletedAt = DateTimeOffset.UtcNow;

            // Soft-delete active child tasks with the same timestamp for cascade-restore matching.
            var activeChildTasks = (await taskRepository.Get(t => t.ProjectId == id)).ToList();
            foreach (var task in activeChildTasks)
            {
                task.DeletedAt = deletedAt;
                await taskRepository.Update(task);
            }

            projectToDelete.DeletedAt = deletedAt;
            await projectRepository.Update(projectToDelete);

            logger.LogInformation($"Project with Id {projectToDelete.ProjectId} soft-deleted along with {activeChildTasks.Count} tasks.");
            return NoContent();
        }

        // RESTORE: api/Project/{id}/restore
        [HttpPost("{id}/restore")]
        public async Task<ActionResult> RestoreProject(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var deleted = (await projectRepository.GetDeleted(p => p.ProjectId == id && p.UserId == userId)).FirstOrDefault();
            if (deleted == null)
            {
                return NotFound("Project not found!");
            }

            var projectDeletedAt = deleted.DeletedAt!.Value;

            // Restore child tasks whose DeletedAt matches the project's (cascaded together).
            var siblingTasks = (await taskRepository.GetDeleted(t => t.ProjectId == id && t.DeletedAt == projectDeletedAt)).ToList();
            foreach (var task in siblingTasks)
            {
                task.DeletedAt = null;
                await taskRepository.Update(task);
            }

            await projectRepository.Restore(id);

            logger.LogInformation($"Project with Id {id} restored along with {siblingTasks.Count} tasks.");
            return NoContent();
        }

        // GET: api/Project/trash
        [HttpGet("trash")]
        public async Task<ActionResult<IEnumerable<TrashItemDto>>> GetTrashProjects()
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var retentionDays = configuration.GetValue<int>("TrashRetention:Days", 30);
            var now = DateTimeOffset.UtcNow;

            var deleted = await projectRepository.GetDeleted(p => p.UserId == userId);

            var items = deleted.Select(p => new TrashItemDto
            {
                Id = p.ProjectId,
                Name = p.Name,
                DeletedAt = p.DeletedAt!.Value,
                DaysRemaining = Math.Max(0, (int)Math.Ceiling(retentionDays - (now - p.DeletedAt.Value).TotalDays))
            });

            return Ok(items);
        }

        // PURGE: api/Project/{id}/purge
        [HttpDelete("{id}/purge")]
        public async Task<ActionResult> PurgeProject(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var deleted = (await projectRepository.GetDeleted(p => p.ProjectId == id && p.UserId == userId)).FirstOrDefault();
            if (deleted == null)
            {
                return NotFound("Project not found!");
            }

            await projectRepository.Delete(id);

            logger.LogInformation($"Project with Id {id} was purged.");
            return NoContent();
        }

        // UPDATE: api/Project
        [HttpPut]
        public async Task<ActionResult<ProjectDto>> UpdateProject([FromBody] UpdateProjectDto project)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var foundProject = await projectRepository.GetById(project.ProjectId);

            if (foundProject == null)
            {
                logger.LogError("Project was not found");
                return NotFound("Project not found!");
            }

            if (foundProject.UserId != userId)
            {
                return NotFound("Project not found!");
            }

            var updatedProject = mapper.Map(project, foundProject);

            await projectRepository.Update(updatedProject);

            return Ok(mapper.Map<ProjectDto>(updatedProject));
        }
    }
}