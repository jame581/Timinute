using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Server.Controllers
{
    [Authorize]
    [ApiController]
    [ValidateAntiForgeryToken]
    [Route("[controller]")]
    public class TrackedTaskController : ControllerBase
    {
        private readonly IRepository<TrackedTask> taskRepository;
        private readonly IMapper mapper;
        private readonly ILogger<TrackedTaskController> logger;

        public TrackedTaskController(IRepository<TrackedTask> taskRepository, IMapper mapper, ILogger<TrackedTaskController> logger)
        {
            this.taskRepository = taskRepository;
            this.mapper = mapper;
            this.logger = logger;
        }

        // GET: api/TrackedTasks
        [HttpGet(Name = "TrackedTasks")]
        public async Task<ActionResult<IEnumerable<TrackedTaskDto>>> GetTrackedTasks()
        {
            var companyList = await taskRepository.Get();
            return Ok(mapper.Map<IEnumerable<TrackedTaskDto>>(companyList));
        }

        // GET: api/TrackedTask
        [HttpGet("{id}")]
        public async Task<ActionResult<TrackedTaskDto>> GetTrackedTask(string id)
        {
            var company = await taskRepository.GetById(id);
            if (company == null)
            {
                return NotFound("Tracked task not found!");
            }
            return Ok(mapper.Map<TrackedTaskDto>(company));
        }

        // CREATE: api/TrackedTask
        [HttpPost]
        public async Task<ActionResult<TrackedTaskDto>> CreateTrackedTask([FromBody] CreateTrackedTaskDto trackedTask)
        {
            var newTrackedTask = mapper.Map<TrackedTask>(trackedTask);

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
                var trackedTaskToDelete = await taskRepository.GetById(id);
                if (trackedTaskToDelete == null)
                {
                    logger.LogError("Tracked task was not found");
                    return NotFound("Tracked task not found!");
                }

                await taskRepository.Delete(trackedTaskToDelete);

                logger.LogInformation($"Tracked task with Id {trackedTaskToDelete.TaskId}, Name {trackedTaskToDelete.Name} was deleted.");
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
                var foundTrackedTask = await taskRepository.GetById(trackedTask.TaskId);

                if (foundTrackedTask == null)
                {
                    logger.LogError("Tracked task was not found");
                    return NotFound("Tracked task not found!");
                }

                var updatedTrackedTask = mapper.Map(trackedTask, foundTrackedTask);

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
