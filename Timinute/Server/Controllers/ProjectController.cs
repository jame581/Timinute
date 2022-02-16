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
                return Unauthorized();
            }

            await projectRepository.Delete(projectToDelete);

            logger.LogInformation($"Project with Id {projectToDelete.ProjectId}, Name {projectToDelete.Name} was deleted.");
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
                return Unauthorized();
            }

            var updatedProject = mapper.Map(project, foundProject);

            await projectRepository.Update(updatedProject);

            return Ok(mapper.Map<ProjectDto>(updatedProject));
        }
    }
}