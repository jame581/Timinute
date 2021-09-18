using AutoMapper;
using Timinute.Server.Models;
using Timinute.Shared.Dtos;

namespace Timinute.Server
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Company model
            CreateMap<Company, CompanyDto>();
            CreateMap<CompanyDto, Company>();
            
            // TrackedTask model
            CreateMap<TrackedTask, TrackedTaskDto>();
            CreateMap<TrackedTaskDto, TrackedTask>();
        }
    }
}
