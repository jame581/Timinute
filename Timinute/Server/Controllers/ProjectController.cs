using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Shared.Dtos.Project;

namespace Timinute.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class ProjectController : ControllerBase
    {
        private readonly IRepository<Project> projectRepository;
        private readonly IMapper mapper;
        private readonly ILogger<ProjectController> logger;

        public ProjectController(IRepositoryFactory repositoryFactory, IMapper mapper, ILogger<ProjectController> logger)
        {
            this.mapper = mapper;
            this.logger = logger;

            projectRepository = repositoryFactory.GetRepository<Project>();
        }

        // GET: api/Projects
        [HttpGet(Name = "Projects")]
        public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjects()
        {
            // get current user ID
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var projectList = await projectRepository.Get();
            return Ok(mapper.Map<IEnumerable<ProjectDto>>(projectList));
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
            if (project == null)
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

            try
            {
                await projectRepository.Insert(newProject);
                return Ok(mapper.Map<ProjectDto>(newProject));
            }
            catch (Exception ex)
            {
                //TODO: Add proper expceptions for proper requests
                logger.LogError($"Error when creating Project:", ex.Message);
                return BadRequest("Invalid Project data.");
            }
        }

        // DELETE: api/Project
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteProject(string id)
        {
            try
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

                await projectRepository.Delete(projectToDelete);

                logger.LogInformation($"Project with Id {projectToDelete.ProjectId}, Name {projectToDelete.Name} was deleted.");
                return NoContent();
            }
            catch (Exception ex)
            {
                //TODO: Add proper expceptions for proper requests
                logger.LogError($"Error when creating Project:", ex.Message);
                return BadRequest("Invalid Project data.");
            }
        }

        // UPDATE: api/Project
        [HttpPut]
        public async Task<ActionResult<ProjectDto>> UpdateProject([FromBody] UpdateProjectDto project)
        {
            try
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

                var updatedProject = mapper.Map(project, foundProject);

                await projectRepository.Update(updatedProject);

                return Ok(mapper.Map<ProjectDto>(updatedProject));
            }
            catch (Exception ex)
            {
                //TODO: Add proper expceptions for proper requests
                logger.LogError($"Error when creating Project:", ex.Message);
                return BadRequest("Invalid Project data.");
            }
        }
    }
}