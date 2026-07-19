using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Timinute.Server.Services.App;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Server.Mcp
{
    /// <summary>
    /// MCP tools over the caller's tracked time entries. Constructed per tool call inside the
    /// HTTP request's DI scope; <see cref="McpUserContext.UserId"/> is resolved per call, never
    /// cached in a field. Write tools call <see cref="McpUserContext.RequireWrite"/> first.
    /// Domain exceptions surface as <see cref="McpException"/> so the client sees a clean message.
    /// </summary>
    [McpServerToolType]
    public class TimeEntryTools
    {
        private readonly ITimeEntryAppService timeEntries;
        private readonly McpUserContext user;

        public TimeEntryTools(ITimeEntryAppService timeEntries, McpUserContext user)
        {
            this.timeEntries = timeEntries;
            this.user = user;
        }

        [McpServerTool(Name = "search_time_entries"), Description("Search the current user's time entries, newest first, optionally filtered by date range, project, and name.")]
        public async Task<object> SearchTimeEntries(
            [Description("Optional inclusive start of the range (filters on entry start date).")] DateTimeOffset? from = null,
            [Description("Optional inclusive end of the range (filters on entry start date).")] DateTimeOffset? to = null,
            [Description("Optional project id to filter by.")] string? projectId = null,
            [Description("Optional case-insensitive substring to match against the entry name.")] string? search = null)
        {
            return await timeEntries.SearchAsync(user.UserId, new TimeEntryQuery
            {
                From = from,
                To = to,
                ProjectId = projectId,
                Search = search
            });
        }

        [McpServerTool(Name = "log_time"), Description("Log a new time entry for the current user. Requires a read_write token.")]
        public async Task<object> LogTime(
            [Description("Entry name / description (2-50 characters).")] string name,
            [Description("When the entry started (an absolute timestamp; include the offset).")] DateTimeOffset startDate,
            [Description("Duration in minutes; must be greater than zero.")] int durationMinutes,
            [Description("Optional project id to associate the entry with; must belong to the caller.")] string? projectId = null)
        {
            user.RequireWrite();

            if (durationMinutes <= 0)
            {
                throw new McpException("Duration must be greater than zero.");
            }

            try
            {
                return await timeEntries.LogAsync(user.UserId, new CreateTrackedTaskDto
                {
                    Name = name,
                    StartDate = startDate,
                    Duration = TimeSpan.FromMinutes(durationMinutes),
                    ProjectId = projectId
                });
            }
            catch (AppValidationException ex)
            {
                throw new McpException(ex.Message);
            }
            catch (ProjectOwnershipException)
            {
                throw new McpException("The specified project was not found for this user.");
            }
        }

        [McpServerTool(Name = "update_time_entry"), Description("Update an existing time entry owned by the current user. Requires a read_write token.")]
        public async Task<object> UpdateTimeEntry(
            [Description("Id of the time entry to update.")] string id,
            [Description("New entry name / description (2-50 characters).")] string name,
            [Description("New start timestamp (include the offset).")] DateTimeOffset startDate,
            [Description("Optional new end timestamp; must be strictly after the start. Omit to leave the duration unchanged.")] DateTimeOffset? endDate = null,
            [Description("Optional project id to associate the entry with; must belong to the caller.")] string? projectId = null)
        {
            user.RequireWrite();

            try
            {
                var updated = await timeEntries.UpdateAsync(user.UserId, id, new UpdateTrackedTaskDto
                {
                    TaskId = id,
                    Name = name,
                    StartDate = startDate,
                    EndDate = endDate,
                    ProjectId = projectId
                });

                if (updated is null)
                {
                    throw new McpException("Time entry not found.");
                }

                return updated;
            }
            catch (AppValidationException ex)
            {
                throw new McpException(ex.Message);
            }
            catch (ProjectOwnershipException)
            {
                throw new McpException("The specified project was not found for this user.");
            }
            catch (InvalidTimeRangeException ex)
            {
                throw new McpException(ex.Message);
            }
        }

        [McpServerTool(Name = "delete_time_entry"), Description("Delete (soft-delete) a time entry owned by the current user. Requires a read_write token.")]
        public async Task<object> DeleteTimeEntry(
            [Description("Id of the time entry to delete.")] string id)
        {
            user.RequireWrite();

            var deleted = await timeEntries.DeleteAsync(user.UserId, id);
            if (!deleted)
            {
                throw new McpException("Time entry not found.");
            }

            return new { id, deleted = true };
        }
    }
}
