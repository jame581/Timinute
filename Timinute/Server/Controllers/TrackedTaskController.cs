using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Timinute.Server.Data;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Models.Paging;
using Timinute.Server.Repository;
using Timinute.Shared.Dtos.Paging;
using Timinute.Shared.Dtos.TrackedTask;
using Timinute.Shared.Dtos.Trash;

namespace Timinute.Server.Controllers
{
    [Authorize]
    [ApiController]
    //[ValidateAntiForgeryToken]
    [Route("[controller]")]
    public class TrackedTaskController : ControllerBase
    {
        private readonly IRepository<TrackedTask> taskRepository;
        private readonly IMapper mapper;
        private readonly ILogger<TrackedTaskController> logger;
        private readonly IConfiguration configuration;
        private readonly ApplicationDbContext dbContext;

        public TrackedTaskController(IRepositoryFactory repositoryFactory, IMapper mapper, ILogger<TrackedTaskController> logger, IConfiguration configuration, ApplicationDbContext dbContext)
        {
            this.mapper = mapper;
            this.logger = logger;
            this.configuration = configuration;
            this.dbContext = dbContext;

            taskRepository = repositoryFactory.GetRepository<TrackedTask>();
        }

        // GET: api/TrackedTasks
        [HttpGet(Name = "TrackedTasks")]
        public async Task<ActionResult<IEnumerable<TrackedTaskDto>>> GetTrackedTasks([FromQuery] PagingParameters trackedTaskParameters)
        {
            // get current user ID
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var pagedTrackedTaskList = await taskRepository.GetPaged(trackedTaskParameters,
                trackedTask => trackedTask.UserId == userId,
                orderBy: $"{nameof(TrackedTask.StartDate)} desc",
                includeProperties: $"{nameof(TrackedTask.Project)},{nameof(TrackedTask.Tags)}");

            var metadata = new PaginationHeaderDto
            {
                TotalCount = pagedTrackedTaskList.TotalCount,
                PageSize = pagedTrackedTaskList.PageSize,
                CurrentPage = pagedTrackedTaskList.CurrentPage,
                TotalPages = pagedTrackedTaskList.TotalPages,
                HasNext = pagedTrackedTaskList.HasNext,
                HasPrevious = pagedTrackedTaskList.HasPrevious
            };

            Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(metadata));
            return Ok(mapper.Map<IEnumerable<TrackedTaskDto>>(pagedTrackedTaskList));
        }

        // GET: api/TrackedTask/search
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<TrackedTaskDto>>> SearchTrackedTasks(
            [FromQuery] PagingParameters pagingParameters,
            [FromQuery] DateTimeOffset? from = null,
            [FromQuery] DateTimeOffset? to = null,
            [FromQuery] string? projectId = null,
            [FromQuery] string? search = null,
            [FromQuery] List<string>? tagIds = null)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var normalizedProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim();
            var normalizedTagIds = NormalizeTagIds(tagIds);

            var pagedTrackedTaskList = await taskRepository.GetPaged(pagingParameters,
                t => t.UserId == userId
                    && (from == null || t.StartDate >= from.Value.ToUniversalTime())
                    && (to == null || t.StartDate <= to.Value.ToUniversalTime())
                    && (normalizedProjectId == null || t.ProjectId == normalizedProjectId)
                    && (normalizedSearch == null || t.Name.Contains(normalizedSearch))
                    && (normalizedTagIds == null || t.Tags.Any(tag => normalizedTagIds.Contains(tag.TagId))),
                orderBy: $"{nameof(TrackedTask.StartDate)} desc",
                includeProperties: $"{nameof(TrackedTask.Project)},{nameof(TrackedTask.Tags)}");

            var metadata = new PaginationHeaderDto
            {
                TotalCount = pagedTrackedTaskList.TotalCount,
                PageSize = pagedTrackedTaskList.PageSize,
                CurrentPage = pagedTrackedTaskList.CurrentPage,
                TotalPages = pagedTrackedTaskList.TotalPages,
                HasNext = pagedTrackedTaskList.HasNext,
                HasPrevious = pagedTrackedTaskList.HasPrevious
            };

            Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(metadata));
            return Ok(mapper.Map<IEnumerable<TrackedTaskDto>>(pagedTrackedTaskList));
        }

        // GET: api/TrackedTask
        [HttpGet("{id}")]
        public async Task<ActionResult<TrackedTaskDto>> GetTrackedTask(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var trackedTask = await taskRepository.GetByIdInclude(
                t => t.TaskId == id && t.UserId == userId,
                includeProperties: $"{nameof(TrackedTask.Project)},{nameof(TrackedTask.Tags)}");
            if (trackedTask == null)
            {
                return NotFound("Tracked task not found!");
            }
            return Ok(mapper.Map<TrackedTaskDto>(trackedTask));
        }

        // CREATE: api/TrackedTask
        [HttpPost]
        public async Task<ActionResult<TrackedTaskDto>> CreateTrackedTask([FromBody] CreateTrackedTaskDto trackedTask)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Whitespace means "no project"; trim otherwise — SQL Server's trailing-space padding would
            // let "ProjectId1 " pass the ownership check and persist untrimmed in the FK column.
            trackedTask.ProjectId = string.IsNullOrWhiteSpace(trackedTask.ProjectId) ? null : trackedTask.ProjectId.Trim();

            if (!await ProjectBelongsToUserAsync(userId, trackedTask.ProjectId))
            {
                return NotFound("Project not found!");
            }

            var newTrackedTask = mapper.Map<TrackedTask>(trackedTask);
            newTrackedTask.UserId = userId;
            newTrackedTask.StartDate = trackedTask.StartDate.ToUniversalTime();
            newTrackedTask.EndDate = newTrackedTask.StartDate + newTrackedTask.Duration;
            newTrackedTask.Tags = await ResolveTagsAsync(userId, trackedTask.TagIds);

            await taskRepository.Insert(newTrackedTask);
            return Ok(mapper.Map<TrackedTaskDto>(newTrackedTask));
        }

        // DELETE: api/TrackedTask
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTrackedTask(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var trackedTaskToDelete = await taskRepository.GetByIdInclude(t => t.TaskId == id && t.UserId == userId);
            if (trackedTaskToDelete == null)
            {
                logger.LogError("Tracked task was not found");
                return NotFound("Tracked task not found!");
            }

            await taskRepository.SoftDelete(id);

            logger.LogInformation("Tracked task with Id {TaskId} was soft-deleted.", id);
            return NoContent();

        }

        // RESTORE: api/TrackedTask/{id}/restore
        [HttpPost("{id}/restore")]
        public async Task<ActionResult> RestoreTrackedTask(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var deleted = (await taskRepository.GetDeleted(t => t.TaskId == id && t.UserId == userId)).FirstOrDefault();
            if (deleted == null)
            {
                return NotFound("Tracked task not found!");
            }

            await taskRepository.Restore(id);

            logger.LogInformation("Tracked task with Id {TaskId} was restored.", id);
            return NoContent();
        }

        // GET: api/TrackedTask/trash
        [HttpGet("trash")]
        public async Task<ActionResult<IEnumerable<TrashItemDto>>> GetTrashTrackedTasks()
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var retentionDays = configuration.GetValue<int>("TrashRetention:Days", 30);
            var now = DateTimeOffset.UtcNow;

            var deleted = await taskRepository.GetDeleted(t => t.UserId == userId);

            var items = deleted.Select(t => new TrashItemDto
            {
                Id = t.TaskId,
                Name = t.Name,
                DeletedAt = t.DeletedAt!.Value,
                DaysRemaining = Math.Max(0, (int)Math.Ceiling(retentionDays - (now - t.DeletedAt.Value).TotalDays))
            });

            return Ok(items);
        }

        // PURGE: api/TrackedTask/{id}/purge
        [HttpDelete("{id}/purge")]
        public async Task<ActionResult> PurgeTrackedTask(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var deleted = (await taskRepository.GetDeleted(t => t.TaskId == id && t.UserId == userId)).FirstOrDefault();
            if (deleted == null)
            {
                return NotFound("Tracked task not found!");
            }

            await taskRepository.Delete(id);

            logger.LogInformation("Tracked task with Id {TaskId} was purged.", id);
            return NoContent();
        }

        // UPDATE: api/TrackedTask
        [HttpPut]
        public async Task<ActionResult<TrackedTaskDto>> UpdateTrackedTask([FromBody] UpdateTrackedTaskDto trackedTask)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var foundTrackedTask = await dbContext.TrackedTasks
                .Include(t => t.Tags)
                .AsTracking()
                .FirstOrDefaultAsync(t => t.TaskId == trackedTask.TaskId && t.UserId == userId);

            if (foundTrackedTask == null)
            {
                logger.LogError("Tracked task was not found");
                return NotFound("Tracked task not found!");
            }

            // Whitespace means "no project"; trim otherwise — SQL Server's trailing-space padding would
            // let "ProjectId1 " pass the ownership check and persist untrimmed in the FK column.
            trackedTask.ProjectId = string.IsNullOrWhiteSpace(trackedTask.ProjectId) ? null : trackedTask.ProjectId.Trim();

            if (!await ProjectBelongsToUserAsync(userId, trackedTask.ProjectId))
            {
                return NotFound("Project not found!");
            }

            var updatedTrackedTask = mapper.Map(trackedTask, foundTrackedTask);
            updatedTrackedTask.StartDate = updatedTrackedTask.StartDate.ToUniversalTime();

            if (updatedTrackedTask.EndDate.HasValue)
            {
                updatedTrackedTask.EndDate = updatedTrackedTask.EndDate.Value.ToUniversalTime();

                if (updatedTrackedTask.EndDate.Value <= updatedTrackedTask.StartDate)
                {
                    return BadRequest("End date must be strictly after start date.");
                }

                updatedTrackedTask.Duration = updatedTrackedTask.EndDate.Value - updatedTrackedTask.StartDate;
            }

            if (trackedTask.TagIds != null)
            {
                var resolvedTags = await ResolveTagsAsync(userId, trackedTask.TagIds);
                updatedTrackedTask.Tags.Clear();
                foreach (var tag in resolvedTags)
                {
                    updatedTrackedTask.Tags.Add(tag);
                }
            }

            await dbContext.SaveChangesAsync();
            var updatedTrackedTaskEntity = await dbContext.TrackedTasks
                .AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.Tags)
                .Where(t => t.TaskId == updatedTrackedTask.TaskId && t.UserId == userId)
                .SingleOrDefaultAsync();

            if (updatedTrackedTaskEntity == null)
            {
                logger.LogError("Tracked task was not found after update");
                return NotFound("Tracked task not found!");
            }

            return Ok(mapper.Map<TrackedTaskDto>(updatedTrackedTaskEntity));
        }

        private async Task<bool> ProjectBelongsToUserAsync(string userId, string? projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
            {
                return true;
            }

            return await dbContext.Projects.AnyAsync(p => p.ProjectId == projectId && p.UserId == userId);
        }

        private async Task<List<Tag>> ResolveTagsAsync(string userId, IEnumerable<string>? tagIds)
        {
            var normalizedTagIds = NormalizeTagIds(tagIds);
            if (normalizedTagIds == null)
            {
                return new List<Tag>();
            }

            return await dbContext.Tags
                .AsTracking()
                .Where(tag => tag.UserId == userId && normalizedTagIds.Contains(tag.TagId))
                .ToListAsync();
        }

        private static List<string>? NormalizeTagIds(IEnumerable<string>? tagIds)
        {
            if (tagIds == null)
            {
                return null;
            }

            var normalized = tagIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct()
                .ToList();

            return normalized.Count == 0 ? null : normalized;
        }
    }
}
