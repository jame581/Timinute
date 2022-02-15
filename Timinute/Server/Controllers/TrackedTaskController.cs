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
using Timinute.Shared.Dtos.TrackedTask;

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

        public TrackedTaskController(IRepositoryFactory repositoryFactory, IMapper mapper, ILogger<TrackedTaskController> logger)
        {
            this.mapper = mapper;
            this.logger = logger;

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

            var pagedTrackedTaskList = await taskRepository.GetPaged(trackedTaskParameters, "Project");

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

            //var trackedTaskList = await taskRepository.Get(x => x.UserId == userId, x => x.OrderByDescending(t => t.StartDate), includeProperties: "Project");
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

            var trackedTask = await taskRepository.GetById(id);
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

            var newTrackedTask = mapper.Map<TrackedTask>(trackedTask);
            newTrackedTask.UserId = userId;
            newTrackedTask.StartDate = trackedTask.StartDate.ToUniversalTime();
            newTrackedTask.EndDate = trackedTask.StartDate + newTrackedTask.Duration;

            try
            {
                await taskRepository.Insert(newTrackedTask);
                return Ok(mapper.Map<TrackedTaskDto>(newTrackedTask));
            }
            catch (Exception ex)
            {
                //TODO: Add proper expceptions for proper requests
                logger.LogError($"Error when creating Tracked task:", ex.Message);
                return BadRequest("Invalid Tracked task data.");
            }
        }

        // DELETE: api/TrackedTask
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTrackedTask(string id)
        {
            try
            {
                var userId = User.FindFirstValue(Constants.Claims.UserId);

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var trackedTaskToDelete = await taskRepository.Find(id);
                if (trackedTaskToDelete == null)
                {
                    logger.LogError("Tracked task was not found");
                    return NotFound("Tracked task not found!");
                }

                if (trackedTaskToDelete.UserId != userId)
                {
                    return Unauthorized();
                }

                await taskRepository.Delete(id);

                logger.LogInformation($"Tracked task with Id {id} was deleted.");
                return NoContent();
            }
            catch (Exception ex)
            {
                //TODO: Add proper expceptions for proper requests
                logger.LogError($"Error when creating Tracked task:", ex.Message);
                return BadRequest("Invalid Tracked task data.");
            }
        }

        // UPDATE: api/TrackedTask
        [HttpPut]
        public async Task<ActionResult<TrackedTaskDto>> UpdateTrackedTask([FromBody] UpdateTrackedTaskDto trackedTask)
        {
            try
            {
                var userId = User.FindFirstValue(Constants.Claims.UserId);

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var foundTrackedTask = await taskRepository.Find(trackedTask.TaskId);

                if (foundTrackedTask == null)
                {
                    logger.LogError("Tracked task was not found");
                    return NotFound("Tracked task not found!");
                }

                if (foundTrackedTask.UserId != userId)
                {
                    return Unauthorized();
                }

                var updatedTrackedTask = mapper.Map(trackedTask, foundTrackedTask);
                updatedTrackedTask.StartDate = updatedTrackedTask.StartDate.ToUniversalTime();

                if (updatedTrackedTask.EndDate.HasValue)
                {
                    updatedTrackedTask.EndDate = updatedTrackedTask.EndDate.Value.ToUniversalTime();
                    updatedTrackedTask.Duration = updatedTrackedTask.EndDate.Value - updatedTrackedTask.StartDate;
                }

                await taskRepository.Update(updatedTrackedTask);

                return Ok(mapper.Map<TrackedTaskDto>(updatedTrackedTask));
            }
            catch (Exception ex)
            {
                //TODO: Add proper expceptions for proper requests
                logger.LogError($"Error when creating Tracked task:", ex.Message);
                return BadRequest("Invalid Tracked task data.");
            }
        }
    }
}
