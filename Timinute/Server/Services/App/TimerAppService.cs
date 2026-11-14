using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Shared.Dtos.Timer;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Server.Services.App
{
    /// <summary>Thrown when the caller already has a running timer. Mapped to HTTP 409.</summary>
    public class TimerConflictException : Exception
    {
    }

    /// <summary>Thrown when the caller has no running timer to stop or discard. Mapped to HTTP 404.</summary>
    public class TimerNotFoundException : Exception
    {
    }

    public interface ITimerAppService
    {
        Task<RunningTimerDto?> GetAsync(string userId);
        Task<RunningTimerDto> StartAsync(string userId, StartTimerDto dto);
        Task<StopTimerResultDto> StopAsync(string userId, StopTimerDto dto);
        Task DiscardAsync(string userId);
    }

    /// <summary>
    /// The server-side running timer. A running timer is a <see cref="RunningTimer"/> row, never a
    /// <see cref="TrackedTask"/>, so analytics/export/search/MCP never see a half-finished entry.
    /// Stopping converts it through <see cref="ITimeEntryAppService.LogAsync"/> (one ownership and
    /// validation path) and deletes the row inside one transaction.
    /// </summary>
    public class TimerAppService : ITimerAppService
    {
        /// <summary>Tolerance for a client-supplied stop time slightly ahead of the server clock.</summary>
        internal static readonly TimeSpan EndAtSkew = TimeSpan.FromSeconds(5);

        private static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(1);

        private readonly ApplicationDbContext db;
        private readonly ITimeEntryAppService timeEntries;
        private readonly IMapper mapper;
        private readonly TimeProvider clock;

        public TimerAppService(ApplicationDbContext db, ITimeEntryAppService timeEntries, IMapper mapper, TimeProvider clock)
        {
            this.db = db;
            this.timeEntries = timeEntries;
            this.mapper = mapper;
            this.clock = clock;
        }

        /// <summary>Test seam: runs between the "already running?" check and the insert.</summary>
        internal Func<Task>? BeforeInsertForTests { get; set; }

        public async Task<RunningTimerDto?> GetAsync(string userId)
        {
            var timer = await db.RunningTimers.AsNoTracking().SingleOrDefaultAsync(t => t.UserId == userId);
            return timer == null ? null : await ToDtoAsync(userId, timer);
        }

        public async Task<RunningTimerDto> StartAsync(string userId, StartTimerDto dto)
        {
            dto.Name = dto.Name?.Trim()!;
            DtoValidator.Validate(dto);

            // Whitespace means "no project"; trim otherwise (SQL Server trailing-space padding would let
            // "Id " pass the ownership check and persist untrimmed).
            var projectId = string.IsNullOrWhiteSpace(dto.ProjectId) ? null : dto.ProjectId.Trim();
            if (projectId != null && !await OwnsLiveProjectAsync(userId, projectId))
            {
                throw new ProjectOwnershipException();
            }

            if (await db.RunningTimers.AnyAsync(t => t.UserId == userId))
            {
                throw new TimerConflictException();
            }

            if (BeforeInsertForTests != null)
            {
                await BeforeInsertForTests();
            }

            var timer = new RunningTimer
            {
                UserId = userId,
                Name = dto.Name,
                ProjectId = projectId,
                StartDate = clock.GetUtcNow(),
            };

            db.RunningTimers.Add(timer);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Lost the race to another tab/device: the PK (UserId) rejected the second row.
                db.Entry(timer).State = EntityState.Detached;
                if (await db.RunningTimers.AnyAsync(t => t.UserId == userId))
                {
                    throw new TimerConflictException();
                }

                throw;
            }

            return await ToDtoAsync(userId, timer);
        }

        public async Task<StopTimerResultDto> StopAsync(string userId, StopTimerDto dto)
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            var timer = await db.RunningTimers.AsTracking().SingleOrDefaultAsync(t => t.UserId == userId)
                ?? throw new TimerNotFoundException();

            var now = clock.GetUtcNow();
            var endAt = now;
            if (dto.EndAt.HasValue)
            {
                endAt = dto.EndAt.Value.ToUniversalTime();
                if (endAt <= timer.StartDate)
                {
                    throw new InvalidTimeRangeException("EndAt must be after the timer's start.");
                }

                if (endAt > now + EndAtSkew)
                {
                    throw new InvalidTimeRangeException("EndAt cannot be in the future.");
                }
            }

            var duration = endAt - timer.StartDate;
            if (duration < MinimumDuration)
            {
                duration = MinimumDuration;
            }

            // The project may have been deleted while the timer ran; keep the time, drop the project.
            var projectId = timer.ProjectId;
            var projectCleared = false;
            if (projectId != null && !await OwnsLiveProjectAsync(userId, projectId))
            {
                projectId = null;
                projectCleared = true;
            }

            TrackedTaskDto created;
            try
            {
                created = await timeEntries.LogAsync(userId, NewEntry(timer, duration, projectId));
            }
            catch (ProjectOwnershipException) when (projectId != null)
            {
                // Trashed between our check and LogAsync's own — same rule: keep the time, drop the project.
                created = await timeEntries.LogAsync(userId, NewEntry(timer, duration, projectId: null));
                projectCleared = true;
            }

            db.RunningTimers.Remove(timer);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // A concurrent stop (another tab/device) already removed the timer. Disposing the
                // uncommitted transaction rolls back the task we just logged, so nothing is duplicated.
                throw new TimerNotFoundException();
            }

            await transaction.CommitAsync();

            return new StopTimerResultDto { TrackedTask = created, ProjectCleared = projectCleared };
        }

        public async Task DiscardAsync(string userId)
        {
            var timer = await db.RunningTimers.AsTracking().SingleOrDefaultAsync(t => t.UserId == userId)
                ?? throw new TimerNotFoundException();

            db.RunningTimers.Remove(timer);
            await db.SaveChangesAsync();
        }

        private static CreateTrackedTaskDto NewEntry(RunningTimer timer, TimeSpan duration, string? projectId) => new()
        {
            Name = timer.Name,
            StartDate = timer.StartDate,
            Duration = duration,
            ProjectId = projectId,
        };

        // The soft-delete global query filter applies, so a trashed project counts as gone.
        private Task<bool> OwnsLiveProjectAsync(string userId, string projectId) =>
            db.Projects.AnyAsync(p => p.ProjectId == projectId && p.UserId == userId);

        private async Task<RunningTimerDto> ToDtoAsync(string userId, RunningTimer timer)
        {
            var dto = mapper.Map<RunningTimerDto>(timer);
            if (timer.ProjectId != null)
            {
                var project = await db.Projects.AsNoTracking()
                    .Where(p => p.ProjectId == timer.ProjectId && p.UserId == userId)
                    .Select(p => new { p.Name, p.Color })
                    .SingleOrDefaultAsync();
                dto.ProjectName = project?.Name;
                dto.ProjectColor = project?.Color;
            }

            return dto;
        }
    }
}
