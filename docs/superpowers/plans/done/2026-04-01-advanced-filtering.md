# Advanced Filtering Implementation Plan

> **Status:** ✅ Shipped — merged via PR #30 on 2026-04-01. `[HttpGet("search")]` endpoints exist on both `TrackedTaskController` and `ProjectController` with date-range, project, name, and task-count filters.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add search/filter endpoints for tracked tasks and projects with date range, project, name search, and task count filters.

**Architecture:** New action methods on existing controllers using the repository's `GetPaged` and `Get` methods. Filter expressions built from optional query parameters combined with AND logic. TDD approach.

**Tech Stack:** .NET 10, EF Core 10, xUnit, Moq, EF Core InMemory

---

### Task 1: Add TrackedTask search endpoint with tests (TDD)

**Files:**
- Modify: `Timinute/Server/Controllers/TrackedTaskController.cs`
- Modify: `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs`

- [ ] **Step 1: Write 5 failing tests**

Add to `TrackedTaskControllerTest.cs` before the `CreateController` method:

```csharp
[Fact]
public async Task Search_Tasks_By_DateRange()
{
    TrackedTaskController controller = await CreateController();
    var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

    var from = new DateTimeOffset(2021, 10, 1, 0, 0, 0, TimeSpan.Zero);
    var to = new DateTimeOffset(2021, 10, 31, 0, 0, 0, TimeSpan.Zero);

    var actionResult = await controller.SearchTrackedTasks(pagingParams, from, to, null, null);

    Assert.NotNull(actionResult);
    Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
    var okResult = actionResult.Result as OkObjectResult;
    var tasks = okResult!.Value as IEnumerable<TrackedTaskDto>;
    Assert.NotNull(tasks);
    Assert.Equal(4, tasks!.Count()); // ApplicationUser1 has 4 tasks on 2021-10-01
}

[Fact]
public async Task Search_Tasks_By_ProjectId()
{
    TrackedTaskController controller = await CreateController();
    var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

    var actionResult = await controller.SearchTrackedTasks(pagingParams, null, null, "ProjectId1", null);

    Assert.NotNull(actionResult);
    var okResult = actionResult.Result as OkObjectResult;
    var tasks = okResult!.Value as IEnumerable<TrackedTaskDto>;
    Assert.NotNull(tasks);
    Assert.Equal(3, tasks!.Count()); // ProjectId1 has tasks 1,2,3
}

[Fact]
public async Task Search_Tasks_By_Name()
{
    TrackedTaskController controller = await CreateController();
    var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

    var actionResult = await controller.SearchTrackedTasks(pagingParams, null, null, null, "Task 1");

    Assert.NotNull(actionResult);
    var okResult = actionResult.Result as OkObjectResult;
    var tasks = okResult!.Value as IEnumerable<TrackedTaskDto>;
    Assert.NotNull(tasks);
    Assert.Single(tasks!);
    Assert.Equal("TrackedTaskId1", tasks!.First().TaskId);
}

[Fact]
public async Task Search_Tasks_Combined_Filters()
{
    TrackedTaskController controller = await CreateController();
    var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

    var from = new DateTimeOffset(2021, 10, 1, 0, 0, 0, TimeSpan.Zero);
    var to = new DateTimeOffset(2021, 10, 31, 0, 0, 0, TimeSpan.Zero);

    var actionResult = await controller.SearchTrackedTasks(pagingParams, from, to, "ProjectId1", "Task");

    Assert.NotNull(actionResult);
    var okResult = actionResult.Result as OkObjectResult;
    var tasks = okResult!.Value as IEnumerable<TrackedTaskDto>;
    Assert.NotNull(tasks);
    Assert.Equal(3, tasks!.Count()); // ProjectId1 tasks within date range matching "Task"
}

[Fact]
public async Task Search_Tasks_Another_User_Empty()
{
    ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "SearchAuthTest");
    TrackedTaskController controller = await CreateController(applicationDbContext, "NonExistentUser");
    var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

    var actionResult = await controller.SearchTrackedTasks(pagingParams, null, null, null, null);

    Assert.NotNull(actionResult);
    var okResult = actionResult.Result as OkObjectResult;
    var tasks = okResult!.Value as IEnumerable<TrackedTaskDto>;
    Assert.NotNull(tasks);
    Assert.Empty(tasks!);
}
```

Add required using if not present: `using System.Linq;`

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test --filter "FullyQualifiedName~Search_Tasks"
```
Expected: FAIL (SearchTrackedTasks method doesn't exist)

- [ ] **Step 3: Implement SearchTrackedTasks**

Add to `TrackedTaskController.cs` after the `GetTrackedTasks` method:

```csharp
// GET: api/TrackedTask/search
[HttpGet("search")]
public async Task<ActionResult<IEnumerable<TrackedTaskDto>>> SearchTrackedTasks(
    [FromQuery] PagingParameters pagingParameters,
    [FromQuery] DateTimeOffset? from = null,
    [FromQuery] DateTimeOffset? to = null,
    [FromQuery] string? projectId = null,
    [FromQuery] string? search = null)
{
    var userId = User.FindFirstValue(Constants.Claims.UserId);

    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized();
    }

    var pagedTrackedTaskList = await taskRepository.GetPaged(pagingParameters,
        t => t.UserId == userId
            && (from == null || t.StartDate >= from.Value.ToUniversalTime())
            && (to == null || t.StartDate <= to.Value.ToUniversalTime())
            && (projectId == null || t.ProjectId == projectId)
            && (search == null || t.Name.Contains(search, StringComparison.OrdinalIgnoreCase)),
        orderBy: $"{nameof(TrackedTask.StartDate)} desc",
        includeProperties: "Project");

    var metadata = new PaginationHeaderDto
    {
        TotalCount = pagedTrackedTaskList.TotalCount,
        PageSize = pagedTrackedTaskList.PageSize,
        CurrentPage = pagedTrackedTaskList.CurrentPage,
        TotalPages = pagedTrackedTaskList.TotalPages,
        HasNext = pagedTrackedTaskList.HasNext,
        HasPrevious = pagedTrackedTaskList.HasPrevious
    };

    Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(metadata));
    return Ok(mapper.Map<IEnumerable<TrackedTaskDto>>(pagedTrackedTaskList));
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test --filter "FullyQualifiedName~Search_Tasks"
```
Expected: 5/5 PASS

- [ ] **Step 5: Run all tests**

```bash
dotnet test
```
Expected: 65/65 pass (60 existing + 5 new)

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server/Controllers/TrackedTaskController.cs Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs
git commit -m "feat: add TrackedTask search endpoint with date, project, and name filters"
```

---

### Task 2: Add Project search endpoint with tests (TDD)

**Files:**
- Modify: `Timinute/Server/Controllers/ProjectController.cs`
- Modify: `Timinute/Server.Tests/Controllers/ProjectControllerTest.cs`

- [ ] **Step 1: Write 3 failing tests**

Add to `ProjectControllerTest.cs` before the `CreateController` method:

```csharp
[Fact]
public async Task Search_Projects_By_Name()
{
    ProjectController controller = await CreateController();
    var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

    var actionResult = await controller.SearchProjects(pagingParams, "Project 1", null);

    Assert.NotNull(actionResult);
    Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
    var okResult = actionResult.Result as OkObjectResult;
    var projects = okResult!.Value as IEnumerable<ProjectDto>;
    Assert.NotNull(projects);
    Assert.Single(projects!);
    Assert.Equal("ProjectId1", projects!.First().ProjectId);
}

[Fact]
public async Task Search_Projects_By_MinTaskCount()
{
    ProjectController controller = await CreateController();
    var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

    var actionResult = await controller.SearchProjects(pagingParams, null, 1);

    Assert.NotNull(actionResult);
    var okResult = actionResult.Result as OkObjectResult;
    var projects = okResult!.Value as IEnumerable<ProjectDto>;
    Assert.NotNull(projects);
    // ApplicationUser1: ProjectId1 has 3 tasks, ProjectId4/5 have 0
    Assert.Single(projects!);
    Assert.Equal("ProjectId1", projects!.First().ProjectId);
}

[Fact]
public async Task Search_Projects_Another_User_Empty()
{
    ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "SearchAuthTest");
    ProjectController controller = await CreateController(applicationDbContext, "NonExistentUser");
    var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

    var actionResult = await controller.SearchProjects(pagingParams, null, null);

    Assert.NotNull(actionResult);
    var okResult = actionResult.Result as OkObjectResult;
    var projects = okResult!.Value as IEnumerable<ProjectDto>;
    Assert.NotNull(projects);
    Assert.Empty(projects!);
}
```

Add required using if not present: `using System.Linq;`

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test --filter "FullyQualifiedName~Search_Projects"
```
Expected: FAIL (SearchProjects method doesn't exist)

- [ ] **Step 3: Implement SearchProjects**

Add to `ProjectController.cs` after the `GetProjects` method:

```csharp
// GET: api/Project/search
[HttpGet("search")]
public async Task<ActionResult<IEnumerable<ProjectDto>>> SearchProjects(
    [FromQuery] PagingParameters pagingParameters,
    [FromQuery] string? search = null,
    [FromQuery] int? minTaskCount = null)
{
    var userId = User.FindFirstValue(Constants.Claims.UserId);

    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized();
    }

    var projects = await projectRepository.Get(
        p => p.UserId == userId
            && (search == null || p.Name.Contains(search)),
        includeProperties: nameof(Project.TrackedTasks));

    if (minTaskCount.HasValue)
    {
        projects = projects.Where(p => (p.TrackedTasks?.Count ?? 0) >= minTaskCount.Value);
    }

    var projectList = projects.ToList();
    var totalCount = projectList.Count;
    var pagedProjects = projectList
        .Skip((pagingParameters.PageNumber - 1) * pagingParameters.PageSize)
        .Take(pagingParameters.PageSize)
        .ToList();

    var metadata = new PaginationHeaderDto
    {
        TotalCount = totalCount,
        PageSize = pagingParameters.PageSize,
        CurrentPage = pagingParameters.PageNumber,
        TotalPages = (int)Math.Ceiling(totalCount / (double)pagingParameters.PageSize),
        HasNext = pagingParameters.PageNumber * pagingParameters.PageSize < totalCount,
        HasPrevious = pagingParameters.PageNumber > 1
    };

    Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(metadata));
    return Ok(mapper.Map<IEnumerable<ProjectDto>>(pagedProjects));
}
```

Add `using Timinute.Server.Models;` if not already present (for `nameof(Project.TrackedTasks)`).

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test --filter "FullyQualifiedName~Search_Projects"
```
Expected: 3/3 PASS

- [ ] **Step 5: Run all tests**

```bash
dotnet test
```
Expected: 68/68 pass (65 + 3 new)

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server/Controllers/ProjectController.cs Timinute/Server.Tests/Controllers/ProjectControllerTest.cs
git commit -m "feat: add Project search endpoint with name and task count filters"
```

---

### Task 3: Final verification

- [ ] **Step 1: Clean build**

```bash
dotnet clean Timinute.sln && dotnet build Timinute.sln
```
Expected: 0 errors

- [ ] **Step 2: Run full test suite**

```bash
dotnet test --verbosity normal
```
Expected: 68/68 pass

- [ ] **Step 3: Verify git status**

```bash
git status
```
Expected: Clean working tree
