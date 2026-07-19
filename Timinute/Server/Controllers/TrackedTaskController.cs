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
using Timinute.Server.Services.App;
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
        private readonly ITimeEntryAppService timeEntryAppService;

        public TrackedTaskController(IRepositoryFactory repositoryFactory, IMapper mapper, ILogger<TrackedTaskController> logger, IConfiguration configuration, ApplicationDbContext dbContext, ITimeEntryAppService timeEntryAppService)
        {
            this.mapper = mapper;
            this.logger = logger;
            this.configuration = configuration;
            this.timeEntryAppService = timeEntryAppService;

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

            // Share the exact filter definition (and its input normalization) with the
            // service's SearchAsync so the paged controller path and the MCP path can't drift.
            var predicate = TimeEntryAppService.BuildSearchPredicate(userId, from, to, projectId, search, tagIds);

            var pagedTrackedTaskList = await taskRepository.GetPaged(pagingParameters,
                predicate,
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

            try
            {
                var created = await timeEntryAppService.LogAsync(userId, trackedTask);
                return Ok(created);
            }
            catch (ProjectOwnershipException)
            {
                return NotFound("Project not found!");
            }
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

            if (!await timeEntryAppService.DeleteAsync(userId, id))
            {
                logger.LogError("Tracked task was not found");
                return NotFound("Tracked task not found!");
            }

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

            logger.LogInformation("Tracked task with Id {TaskId} was restored.", deleted.TaskId);
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

            logger.LogInformation("Tracked task with Id {TaskId} was purged.", deleted.TaskId);
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

            try
            {
                var updated = await timeEntryAppService.UpdateAsync(userId, trackedTask.TaskId, trackedTask);

                if (updated == null)
                {
                    logger.LogError("Tracked task was not found");
                    return NotFound("Tracked task not found!");
                }

                return Ok(updated);
            }
            catch (ProjectOwnershipException)
            {
                return NotFound("Project not found!");
            }
            catch (InvalidTimeRangeException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
