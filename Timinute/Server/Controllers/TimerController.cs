using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Timinute.Server.Helpers;
using Timinute.Server.Services.App;
using Timinute.Shared.Dtos.Timer;

namespace Timinute.Server.Controllers
{
    /// <summary>
    /// The caller's running timer. Every action is scoped to the authenticated user and there are no
    /// route ids, so nothing here can address another user's timer.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    [ResponseCache(NoStore = true)]
    public class TimerController : ControllerBase
    {
        private readonly ITimerAppService timerAppService;

        public TimerController(ITimerAppService timerAppService)
        {
            this.timerAppService = timerAppService;
        }

        // GET: Timer — 200 with the running timer, 204 when nothing runs.
        [HttpGet]
        public async Task<ActionResult<RunningTimerDto>> Get()
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var timer = await timerAppService.GetAsync(userId);
            return timer == null ? NoContent() : Ok(timer);
        }

        // POST: Timer/start
        [HttpPost("start")]
        public async Task<ActionResult<RunningTimerDto>> Start([FromBody] StartTimerDto dto)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var timer = await timerAppService.StartAsync(userId, dto);
                return StatusCode(StatusCodes.Status201Created, timer);
            }
            catch (TimerConflictException)
            {
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "A timer is already running.");
            }
            catch (AppValidationException ex)
            {
                return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Validation failed.", detail: ex.Message);
            }
            catch (ProjectOwnershipException)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Project not found.");
            }
        }

        // POST: Timer/stop — converts the running timer into a tracked task.
        [HttpPost("stop")]
        public async Task<ActionResult<StopTimerResultDto>> Stop([FromBody] StopTimerDto? dto)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                return Ok(await timerAppService.StopAsync(userId, dto ?? new StopTimerDto()));
            }
            catch (TimerNotFoundException)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "No timer is running.");
            }
            catch (InvalidTimeRangeException ex)
            {
                return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Invalid stop time.", detail: ex.Message);
            }
        }

        // DELETE: Timer — discards the running timer without creating a task.
        [HttpDelete]
        public async Task<ActionResult> Discard()
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                await timerAppService.DiscardAsync(userId);
                return NoContent();
            }
            catch (TimerNotFoundException)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "No timer is running.");
            }
        }
    }
}
