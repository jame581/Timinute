﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Server.Controllers
{
    [Authorize]
    [ApiController]
    //[ValidateAntiForgeryToken]
    [Route("[controller]")]
    public class TrackedTaskController : ControllerBase
    {
        private readonly IRepositoryFactory repositoryFactory;

        private readonly IRepository<TrackedTask> taskRepository;
        private readonly IMapper mapper;
        private readonly ILogger<TrackedTaskController> logger;

        public TrackedTaskController(IRepositoryFactory repositoryFactory, IMapper mapper, ILogger<TrackedTaskController> logger)
        {
            this.repositoryFactory = repositoryFactory;
            this.mapper = mapper;
            this.logger = logger;

            taskRepository = repositoryFactory.GetRepository<TrackedTask>();
        }

        // GET: api/TrackedTasks
        [HttpGet(Name = "TrackedTasks")]
        public async Task<ActionResult<IEnumerable<TrackedTaskDto>>> GetTrackedTasks()
        {
            // get current user ID
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var trackedTaskList = await taskRepository.Get(x => x.UserId == userId);
            return Ok(mapper.Map<IEnumerable<TrackedTaskDto>>(trackedTaskList));
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
            newTrackedTask.Duration = newTrackedTask.EndDate - trackedTask.StartDate;

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

                var trackedTaskToDelete = await taskRepository.GetByIdInclude(x => x.TaskId == id && x.UserId == userId);
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
                var userId = User.FindFirstValue(Constants.Claims.UserId);

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var foundTrackedTask = await taskRepository.GetByIdInclude(x => x.TaskId == trackedTask.TaskId && x.UserId == userId);

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