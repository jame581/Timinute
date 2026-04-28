using AutoMapper;
using Timinute.Server.Models;
using Timinute.Shared.Dtos;
using Timinute.Shared.Dtos.Project;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Server
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // TrackedTask model
            CreateMap<TrackedTask, TrackedTaskDto>();
            CreateMap<TrackedTaskDto, TrackedTask>();

            CreateMap<TrackedTask, CreateTrackedTaskDto>();
            CreateMap<CreateTrackedTaskDto, TrackedTask>();

            CreateMap<TrackedTask, UpdateTrackedTaskDto>();
            CreateMap<UpdateTrackedTaskDto, TrackedTask>();

            // Project model
            CreateMap<Project, ProjectDto>();
            CreateMap<ProjectDto, Project>();

            CreateMap<Project, CreateProjectDto>();
            CreateMap<CreateProjectDto, Project>();

            CreateMap<Project, UpdateProjectDto>();
            CreateMap<UpdateProjectDto, Project>()
                // Don't blank out a saved Color when an update arrives without one.
                // A future "edit name only" flow shouldn't wipe the color picker choice.
                .ForMember(d => d.Color, o => o.Condition(src => !string.IsNullOrWhiteSpace(src.Color)));

            // Application User model
            CreateMap<ApplicationUser, ApplicationUserDto>();
            CreateMap<ApplicationUserDto, ApplicationUser>();

            // User preferences
            CreateMap<UserPreferences, UserPreferencesDto>();
            CreateMap<UserPreferencesDto, UserPreferences>();
            CreateMap<UpdateUserPreferencesDto, UserPreferences>();
        }
    }
}
