using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Shared.Dtos;

namespace Timinute.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IMapper mapper;
        private readonly IRepository<Project> projectRepository;
        private readonly IRepository<TrackedTask> taskRepository;

        public UserController(
            UserManager<ApplicationUser> userManager,
            IMapper mapper,
            IRepositoryFactory repositoryFactory)
        {
            this.userManager = userManager;
            this.mapper = mapper;
            projectRepository = repositoryFactory.GetRepository<Project>();
            taskRepository = repositoryFactory.GetRepository<TrackedTask>();
        }

        // GET: api/User/me
        [HttpGet("me")]
        public async Task<ActionResult<UserProfileDto>> GetMe()
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var tasks = (await taskRepository.Get(t => t.UserId == userId)).ToList();
            var projectCount = (await projectRepository.Get(p => p.UserId == userId)).Count();

            var totalTicks = tasks.Aggregate(0L, (acc, t) => acc + t.Duration.Ticks);

            return Ok(new UserProfileDto
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                CreatedAt = user.CreatedAt,
                TotalTrackedTime = TimeSpan.FromTicks(totalTicks),
                ProjectCount = projectCount,
                TaskCount = tasks.Count,
                Preferences = mapper.Map<UserPreferencesDto>(user.Preferences ?? new UserPreferences())
            });
        }

        // PUT: api/User/me/preferences
        [HttpPut("me/preferences")]
        public async Task<ActionResult<UserPreferencesDto>> UpdatePreferences([FromBody] UpdateUserPreferencesDto dto)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Full-replace semantics: PUT carries every field, AutoMapper
            // copies them onto the owned navigation. Preferences is non-null
            // in production (C# initializer); we still guard for the test
            // path where ApplicationUser may be constructed without it.
            user.Preferences ??= new UserPreferences();
            mapper.Map(dto, user.Preferences);

            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(error.Code, error.Description);
                }
                return BadRequest(ModelState);
            }

            return Ok(mapper.Map<UserPreferencesDto>(user.Preferences));
        }
    }
}
