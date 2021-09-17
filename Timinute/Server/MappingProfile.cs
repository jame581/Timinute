using AutoMapper;
using Timinute.Server.Models;
using Timinute.Shared.Dtos;

namespace Timinute.Server
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Company, CompanyDto>();
            CreateMap<CompanyDto, Company>();
        }
    }
}
