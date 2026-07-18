using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Timinute.Server.Controllers;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Shared.Dtos.Project;

namespace Timinute.Server.Services.App
{
    /// <summary>
    /// Thrown when creating a project would violate the filtered unique index on
    /// (UserId, Name). The controller boundary translates this to HTTP 409 Conflict;
    /// named <c>ProjectNameConflictException</c> (not <c>DuplicateNameException</c>,
    /// which collides with <c>System.Data</c>).
    /// </summary>
    public class ProjectNameConflictException : Exception
    {
    }

    public interface IProjectAppService
    {
        Task<IReadOnlyList<ProjectDto>> ListAsync(string userId);
        Task<ProjectDto> CreateAsync(string userId, CreateProjectDto dto);
    }

    /// <summary>
    /// userId-parameterised project operations shared by <see cref="ProjectController"/>
    /// and the Task 7 MCP tools. Ownership scoping and the round-robin palette default
    /// are lifted verbatim from the controller (create/list). Repository-only per plan R5.
    /// </summary>
    public class ProjectAppService : IProjectAppService
    {
        // Kept identical to ProjectController.ProjectPalette.
        private static readonly string[] Palette =
        {
            "#6366F1", "#F59E0B", "#10B981", "#EC4899", "#94A3B8"
        };

        private readonly IRepository<Project> projects;
        private readonly IMapper mapper;

        public ProjectAppService(IRepositoryFactory factory, IMapper mapper)
        {
            projects = factory.GetRepository<Project>();
            this.mapper = mapper;
        }

        public async Task<IReadOnlyList<ProjectDto>> ListAsync(string userId)
        {
            var list = await projects.Get(p => p.UserId == userId);
            return mapper.Map<List<ProjectDto>>(list);
        }

        public async Task<ProjectDto> CreateAsync(string userId, CreateProjectDto dto)
        {
            var newProject = mapper.Map<Project>(dto);
            newProject.UserId = userId;

            if (string.IsNullOrWhiteSpace(newProject.Color))
            {
                // Count *all* the user's projects including soft-deleted, so the round-robin
                // index keeps advancing past deletions and we don't collide on the palette.
                var existingCount = await projects.CountAll(p => p.UserId == userId);
                newProject.Color = Palette[existingCount % Palette.Length];
            }

            try
            {
                await projects.Insert(newProject);
            }
            catch (DbUpdateException ex) when (ProjectController.IsUniqueConstraintViolation(ex))
            {
                throw new ProjectNameConflictException();
            }

            return mapper.Map<ProjectDto>(newProject);
        }
    }
}
