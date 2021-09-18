using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Shared.Dtos;

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

        public TrackedTaskController(IRepository<TrackedTask> taskRepository, IMapper mapper)
        {
            this.taskRepository = taskRepository;
            this.mapper = mapper;
        }

        // GET: api/TrackedTasks
        [HttpGet(Name = "TrackedTasks")]
        public async Task<ActionResult<IEnumerable<TrackedTaskDto>>> GetCompanies()
        {
            var companyList = await taskRepository.Get();
            return Ok(mapper.Map<IEnumerable<TrackedTaskDto>>(companyList));
        }

        // GET: api/TrackedTask
        [HttpGet("{id}")]
        public async Task<ActionResult<TrackedTaskDto>> GetCompany(string id)
        {
            var company = await taskRepository.GetById(id);
            if (company == null)
            {
                return NotFound("Tracked task not found!");
            }
            return Ok(mapper.Map<TrackedTaskDto>(company));
        }

    }
}
