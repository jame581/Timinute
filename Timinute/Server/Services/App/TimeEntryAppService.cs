using System.Linq.Expressions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Server.Services.App
{
    /// <summary>
    /// Thrown when a client-supplied ProjectId does not belong to the caller.
    /// The controller boundary translates this to HTTP 404 NotFound ("Project not found!").
    /// </summary>
    public class ProjectOwnershipException : Exception
    {
    }

    /// <summary>
    /// Thrown when an update supplies an EndDate that is not strictly after StartDate.
    /// The controller boundary translates this to HTTP 400 BadRequest.
    /// </summary>
    public class InvalidTimeRangeException : Exception
    {
        public InvalidTimeRangeException(string message) : base(message)
        {
        }
    }

    public interface ITimeEntryAppService
    {
        Task<IReadOnlyList<TrackedTaskDto>> SearchAsync(string userId, TimeEntryQuery query);
        Task<TrackedTaskDto> LogAsync(string userId, CreateTrackedTaskDto dto);
        Task<TrackedTaskDto?> UpdateAsync(string userId, string id, UpdateTrackedTaskDto dto);
        Task<bool> DeleteAsync(string userId, string id);
    }

    /// <summary>
    /// userId-parameterised tracked-task operations shared by <c>TrackedTaskController</c>
    /// and the Task 7 MCP tools. Ownership scoping, ProjectId normalization
    /// (whitespace→null + Trim), tag resolution, UTC normalization and EndDate/Duration
    /// computation are lifted verbatim from the controller. Needs the DbContext directly
    /// (tag resolution + project-ownership check) per plan R5.
    /// </summary>
    public class TimeEntryAppService : ITimeEntryAppService
    {
        private readonly IRepository<TrackedTask> taskRepository;
        private readonly IMapper mapper;
        private readonly ApplicationDbContext dbContext;

        public TimeEntryAppService(IRepositoryFactory repositoryFactory, IMapper mapper, ApplicationDbContext dbContext)
        {
            this.mapper = mapper;
            this.dbContext = dbContext;
            taskRepository = repositoryFactory.GetRepository<TrackedTask>();
        }

        /// <summary>
        /// The single source of truth for the tracked-task search predicate, shared by
        /// <see cref="SearchAsync"/> (unpaged, MCP surface) and
        /// <c>TrackedTaskController.SearchTrackedTasks</c> (paged). Performs the same
        /// input normalization for both callers so the filter can never drift.
        /// </summary>
        public static Expression<Func<TrackedTask, bool>> BuildSearchPredicate(
            string userId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? projectId,
            string? search,
            List<string>? tagIds)
        {
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var normalizedProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim();
            var normalizedTagIds = NormalizeTagIds(tagIds);

            return t => t.UserId == userId
                && (from == null || t.StartDate >= from.Value.ToUniversalTime())
                && (to == null || t.StartDate <= to.Value.ToUniversalTime())
                && (normalizedProjectId == null || t.ProjectId == normalizedProjectId)
                && (normalizedSearch == null || t.Name.Contains(normalizedSearch))
                && (normalizedTagIds == null || t.Tags.Any(tag => normalizedTagIds.Contains(tag.TagId)));
        }

        public async Task<IReadOnlyList<TrackedTaskDto>> SearchAsync(string userId, TimeEntryQuery query)
        {
            var predicate = BuildSearchPredicate(userId, query.From, query.To, query.ProjectId, query.Search, query.TagIds);

            var tasks = await taskRepository.Get(
                predicate,
                orderBy: q => q.OrderByDescending(t => t.StartDate),
                includeProperties: $"{nameof(TrackedTask.Project)},{nameof(TrackedTask.Tags)}");

            return mapper.Map<List<TrackedTaskDto>>(tasks);
        }

        public async Task<TrackedTaskDto> LogAsync(string userId, CreateTrackedTaskDto dto)
        {
            // Whitespace means "no project"; trim otherwise — SQL Server's trailing-space padding would
            // let "ProjectId1 " pass the ownership check and persist untrimmed in the FK column.
            dto.ProjectId = string.IsNullOrWhiteSpace(dto.ProjectId) ? null : dto.ProjectId.Trim();

            if (!await ProjectBelongsToUserAsync(userId, dto.ProjectId))
            {
                throw new ProjectOwnershipException();
            }

            var newTrackedTask = mapper.Map<TrackedTask>(dto);
            newTrackedTask.UserId = userId;
            newTrackedTask.StartDate = dto.StartDate.ToUniversalTime();
            newTrackedTask.EndDate = newTrackedTask.StartDate + newTrackedTask.Duration;
            newTrackedTask.Tags = await ResolveTagsAsync(userId, dto.TagIds);

            await taskRepository.Insert(newTrackedTask);
            return mapper.Map<TrackedTaskDto>(newTrackedTask);
        }

        public async Task<TrackedTaskDto?> UpdateAsync(string userId, string id, UpdateTrackedTaskDto dto)
        {
            var foundTrackedTask = await dbContext.TrackedTasks
                .Include(t => t.Tags)
                .AsTracking()
                .FirstOrDefaultAsync(t => t.TaskId == id && t.UserId == userId);

            if (foundTrackedTask == null)
            {
                return null;
            }

            // Whitespace means "no project"; trim otherwise — SQL Server's trailing-space padding would
            // let "ProjectId1 " pass the ownership check and persist untrimmed in the FK column.
            dto.ProjectId = string.IsNullOrWhiteSpace(dto.ProjectId) ? null : dto.ProjectId.Trim();

            if (!await ProjectBelongsToUserAsync(userId, dto.ProjectId))
            {
                throw new ProjectOwnershipException();
            }

            var updatedTrackedTask = mapper.Map(dto, foundTrackedTask);
            updatedTrackedTask.StartDate = updatedTrackedTask.StartDate.ToUniversalTime();

            if (updatedTrackedTask.EndDate.HasValue)
            {
                updatedTrackedTask.EndDate = updatedTrackedTask.EndDate.Value.ToUniversalTime();

                if (updatedTrackedTask.EndDate.Value <= updatedTrackedTask.StartDate)
                {
                    throw new InvalidTimeRangeException("End date must be strictly after start date.");
                }

                updatedTrackedTask.Duration = updatedTrackedTask.EndDate.Value - updatedTrackedTask.StartDate;
            }

            if (dto.TagIds != null)
            {
                var resolvedTags = await ResolveTagsAsync(userId, dto.TagIds);
                updatedTrackedTask.Tags.Clear();
                foreach (var tag in resolvedTags)
                {
                    updatedTrackedTask.Tags.Add(tag);
                }
            }

            await dbContext.SaveChangesAsync();
            var updatedTrackedTaskEntity = await dbContext.TrackedTasks
                .AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.Tags)
                .Where(t => t.TaskId == updatedTrackedTask.TaskId && t.UserId == userId)
                .SingleOrDefaultAsync();

            if (updatedTrackedTaskEntity == null)
            {
                return null;
            }

            return mapper.Map<TrackedTaskDto>(updatedTrackedTaskEntity);
        }

        public async Task<bool> DeleteAsync(string userId, string id)
        {
            var trackedTaskToDelete = await taskRepository.GetByIdInclude(t => t.TaskId == id && t.UserId == userId);
            if (trackedTaskToDelete == null)
            {
                return false;
            }

            await taskRepository.SoftDelete(id);
            return true;
        }

        private async Task<bool> ProjectBelongsToUserAsync(string userId, string? projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
            {
                return true;
            }

            return await dbContext.Projects.AnyAsync(p => p.ProjectId == projectId && p.UserId == userId);
        }

        private async Task<List<Tag>> ResolveTagsAsync(string userId, IEnumerable<string>? tagIds)
        {
            var normalizedTagIds = NormalizeTagIds(tagIds);
            if (normalizedTagIds == null)
            {
                return new List<Tag>();
            }

            return await dbContext.Tags
                .AsTracking()
                .Where(tag => tag.UserId == userId && normalizedTagIds.Contains(tag.TagId))
                .ToListAsync();
        }

        private static List<string>? NormalizeTagIds(IEnumerable<string>? tagIds)
        {
            if (tagIds == null)
            {
                return null;
            }

            var normalized = tagIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct()
                .ToList();

            return normalized.Count == 0 ? null : normalized;
        }
    }
}
