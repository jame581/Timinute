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
        private readonly IRepository<Project> projectRepository;
        private readonly IRepository<TrackedTask> taskRepository;

        public UserController(
            UserManager<ApplicationUser> userManager,
            IRepositoryFactory repositoryFactory)
        {
            this.userManager = userManager;
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
                TaskCount = tasks.Count
            });
        }
    }
}
