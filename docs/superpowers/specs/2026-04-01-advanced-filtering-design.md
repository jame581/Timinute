# Advanced Filtering Design

## Goal

Add search/filter endpoints for tracked tasks and projects with date range, project, name search, and task count filters.

## Scope

- New `GET /trackedtask/search` endpoint with date range, projectId, and name search
- New `GET /project/search` endpoint with name search and minimum task count
- Both paginated, user-scoped, same response format as existing list endpoints
- Tests for all filter combinations and user isolation

Out of scope: full-text search, filter persistence, client-side UI changes.

## New Endpoints

### GET /trackedtask/search

On existing `TrackedTaskController`. `[Authorize]`.

Query params (all optional, combined with AND):
- `from` (`DateTimeOffset?`) — StartDate >= from
- `to` (`DateTimeOffset?`) — StartDate <= to
- `projectId` (`string?`) — exact match on ProjectId
- `search` (`string?`) — case-insensitive Contains on task Name

Also accepts `PagingParameters` (PageNumber, PageSize) from query string.

Returns: `ActionResult<IEnumerable<TrackedTaskDto>>` with `X-Pagination` header.

Sorted by StartDate descending. Includes Project navigation property.

### GET /project/search

On existing `ProjectController`. `[Authorize]`.

Query params (all optional, combined with AND):
- `search` (`string?`) — case-insensitive Contains on project Name
- `minTaskCount` (`int?`) — only projects with at least N tracked tasks

Also accepts `PagingParameters` from query string.

Returns: `ActionResult<IEnumerable<ProjectDto>>` with `X-Pagination` header.

## Implementation

Both endpoints use the existing `IRepository<T>` methods. No new repository code.

**TrackedTask search:** Build a filter expression from query params, pass to `GetPaged` with `orderBy: StartDate desc` and `includeProperties: "Project"`.

**Project search with minTaskCount:** Load user's projects with `TrackedTasks` included via `Get`, filter by name search, filter by task count in-memory, then apply pagination. User-scoped data is small enough for this approach.

## Files to Modify

- `Timinute/Server/Controllers/TrackedTaskController.cs` (add SearchTrackedTasks action)
- `Timinute/Server/Controllers/ProjectController.cs` (add SearchProjects action)
- `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs` (5 new tests)
- `Timinute/Server.Tests/Controllers/ProjectControllerTest.cs` (3 new tests)

## Test Plan

**TrackedTask search (5 tests):**
- `Search_Tasks_By_DateRange` — only tasks within range
- `Search_Tasks_By_ProjectId` — filter by project
- `Search_Tasks_By_Name` — case-insensitive search
- `Search_Tasks_Combined_Filters` — AND logic with multiple params
- `Search_Tasks_Another_User_Empty` — user isolation

**Project search (3 tests):**
- `Search_Projects_By_Name` — case-insensitive search
- `Search_Projects_By_MinTaskCount` — count filtering
- `Search_Projects_Another_User_Empty` — user isolation
