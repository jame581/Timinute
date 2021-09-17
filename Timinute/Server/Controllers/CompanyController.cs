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
    public class CompanyController : ControllerBase
    {
        private readonly IRepository<Company> companyRepository;
        private readonly IMapper mapper;

        public CompanyController(IRepository<Company> companyRepository, IMapper mapper)
        {
            this.companyRepository = companyRepository;
            this.mapper = mapper;
        }

        // GET: api/Categories
        [HttpGet(Name = "GetCompanies")]
        public async Task<ActionResult<IEnumerable<CompanyDto>>> GetAllCompanies()
        {
            var companyList = await companyRepository.Get();
            return Ok(mapper.Map<IEnumerable<CompanyDto>>(companyList));
        }
    }
}
