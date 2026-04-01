# P0: Data Validation & Authorization Fixes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix authorization gaps on GET endpoints and add input validation to all create/update DTOs.

**Architecture:** Data Annotation attributes on DTOs (existing pattern), one custom `MinDurationAttribute` for TimeSpan validation, ownership checks in controller actions, EndDate validation in update controller. TDD approach — tests first, then implementation.

**Tech Stack:** .NET 10, ASP.NET Core Data Annotations, xUnit, Moq, EF Core InMemory

---

### Task 1: Create MinDuration validation attribute

**Files:**
- Create: `Timinute/Shared/Validators/MinDurationAttribute.cs`

- [ ] **Step 1: Create the attribute**

```csharp
using System.ComponentModel.DataAnnotations;

namespace Timinute.Shared.Validators
{
    public class MinDurationAttribute : ValidationAttribute
    {
        public MinDurationAttribute() : base("Duration must be greater than zero.")
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is TimeSpan duration && duration <= TimeSpan.Zero)
            {
                return new ValidationResult(ErrorMessage);
            }

            return ValidationResult.Success;
        }
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Timinute.sln`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add Timinute/Shared/Validators/MinDurationAttribute.cs
git commit -m "feat: add MinDuration validation attribute for TimeSpan"
```

---

### Task 2: Add validation attributes to Project DTOs

**Files:**
- Modify: `Timinute/Shared/Dtos/Project/CreateProjectDto.cs`
- Modify: `Timinute/Shared/Dtos/Project/UpdateProjectDto.cs`

- [ ] **Step 1: Update CreateProjectDto**

Replace full file content:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Timinute.Shared.Dtos.Project
{
    public class CreateProjectDto
    {
        [Required]
        [StringLength(100, ErrorMessage = "Project name must be between 2 and 100 characters.", MinimumLength = 2)]
        public string Name { get; set; } = null!;

        public string? CompanyId { get; set; }
    }
}
```

- [ ] **Step 2: Update UpdateProjectDto**

Replace full file content:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Timinute.Shared.Dtos.Project
{
    public class UpdateProjectDto
    {
        [Required]
        public string ProjectId { get; set; } = null!;

        [Required]
        [StringLength(100, ErrorMessage = "Project name must be between 2 and 100 characters.", MinimumLength = 2)]
        public string Name { get; set; } = null!;
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Timinute.sln`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add Timinute/Shared/Dtos/Project/CreateProjectDto.cs Timinute/Shared/Dtos/Project/UpdateProjectDto.cs
git commit -m "feat: add validation attributes to Project DTOs"
```

---

### Task 3: Add validation attributes to TrackedTask DTOs

**Files:**
- Modify: `Timinute/Shared/Dtos/TrackedTask/CreateTrackedTaskDto.cs`
- Modify: `Timinute/Shared/Dtos/TrackedTask/UpdateTrackedTaskDto.cs`

- [ ] **Step 1: Update CreateTrackedTaskDto**

Replace full file content:

```csharp
using System.ComponentModel.DataAnnotations;
using Timinute.Shared.Dtos.Project;
using Timinute.Shared.Validators;

namespace Timinute.Shared.Dtos.TrackedTask
{
    public class CreateTrackedTaskDto
    {
        [Required]
        [StringLength(50, ErrorMessage = "Name of task is too long.", MinimumLength = 2)]
        public string Name { get; set; } = null!;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        [MinDuration]
        public TimeSpan Duration { get; set; }

        public string? ProjectId { get; set; }

        public ProjectDto? Project { get; set; }
    }
}
```

- [ ] **Step 2: Update UpdateTrackedTaskDto**

Replace full file content:

```csharp
using System.ComponentModel.DataAnnotations;
using Timinute.Shared.Dtos.Project;

namespace Timinute.Shared.Dtos.TrackedTask
{
    public class UpdateTrackedTaskDto
    {
        [Required]
        public string TaskId { get; set; } = null!;

        [Required]
        [StringLength(50, ErrorMessage = "Name of task is too long.", MinimumLength = 2)]
        public string Name { get; set; } = null!;

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string? ProjectId { get; set; }

        public ProjectDto? Project { get; set; }
    }
}
```

- [ ] **Step 3: Verify build and run existing tests**

Run: `dotnet build Timinute.sln && dotnet test`
Expected: 0 errors, 47/47 tests pass

- [ ] **Step 4: Commit**

```bash
git add Timinute/Shared/Dtos/TrackedTask/CreateTrackedTaskDto.cs Timinute/Shared/Dtos/TrackedTask/UpdateTrackedTaskDto.cs
git commit -m "feat: add validation attributes to TrackedTask DTOs"
```

---

### Task 4: Fix authorization on GetProject endpoint

**Files:**
- Modify: `Timinute/Server/Controllers/ProjectController.cs`
- Test: `Timinute/Server.Tests/Controllers/ProjectControllerTest.cs`

- [ ] **Step 1: Write failing test**

Add to `ProjectControllerTest.cs` before the `CreateController` method:

```csharp
[Fact]
public async Task Get_Project_Another_User_Returns_NotFound_Test()
{
    ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "GetAuthTest");
    ProjectController controller = await CreateController(applicationDbContext, "ApplicationUser10");

    // ProjectId1 belongs to ApplicationUser1, not ApplicationUser10
    var actionResult = await controller.GetProject("ProjectId1");

    Assert.NotNull(actionResult);
    Assert.IsAssignableFrom<NotFoundObjectResult>(actionResult.Result);

    var notFoundResult = actionResult.Result as NotFoundObjectResult;
    Assert.NotNull(notFoundResult);
    Assert.Equal("Project not found!", notFoundResult!.Value);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ProjectControllerTest.Get_Project_Another_User_Returns_NotFound_Test"`
Expected: FAIL — returns 200 OK instead of 404

- [ ] **Step 3: Add ownership check to GetProject**

In `ProjectController.cs`, replace the `GetProject` method (lines 62-77):

```csharp
// GET: api/Project
[HttpGet("{id}")]
public async Task<ActionResult<ProjectDto>> GetProject(string id)
{
    var userId = User.FindFirstValue(Constants.Claims.UserId);

    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized();
    }

    var project = await projectRepository.GetById(id);
    if (project == null || project.UserId != userId)
    {
        return NotFound("Project not found!");
    }
    return Ok(mapper.Map<ProjectDto>(project));
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ProjectControllerTest.Get_Project_Another_User_Returns_NotFound_Test"`
Expected: PASS

- [ ] **Step 5: Run all tests to verify no regressions**

Run: `dotnet test`
Expected: 48/48 pass (47 existing + 1 new)

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server/Controllers/ProjectController.cs Timinute/Server.Tests/Controllers/ProjectControllerTest.cs
git commit -m "fix: add ownership check to GetProject endpoint"
```

---

### Task 5: Fix authorization on GetTrackedTask endpoint

**Files:**
- Modify: `Timinute/Server/Controllers/TrackedTaskController.cs`
- Test: `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs`

- [ ] **Step 1: Write failing test**

Add to `TrackedTaskControllerTest.cs` before the `CreateController` method:

```csharp
[Fact]
public async Task Get_TrackedTask_Another_User_Returns_NotFound_Test()
{
    ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "GetAuthTest");
    TrackedTaskController controller = await CreateController(applicationDbContext, "ApplicationUser10");

    // TrackedTaskId1 belongs to ApplicationUser1, not ApplicationUser10
    var actionResult = await controller.GetTrackedTask("TrackedTaskId1");

    Assert.NotNull(actionResult);
    Assert.IsAssignableFrom<NotFoundObjectResult>(actionResult.Result);

    var notFoundResult = actionResult.Result as NotFoundObjectResult;
    Assert.NotNull(notFoundResult);
    Assert.Equal("Tracked task not found!", notFoundResult!.Value);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest.Get_TrackedTask_Another_User_Returns_NotFound_Test"`
Expected: FAIL — returns 200 OK instead of 404

- [ ] **Step 3: Add ownership check to GetTrackedTask**

In `TrackedTaskController.cs`, replace the `GetTrackedTask` method (lines 66-81):

```csharp
// GET: api/TrackedTask
[HttpGet("{id}")]
public async Task<ActionResult<TrackedTaskDto>> GetTrackedTask(string id)
{
    var userId = User.FindFirstValue(Constants.Claims.UserId);

    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized();
    }

    var trackedTask = await taskRepository.GetById(id);
    if (trackedTask == null || trackedTask.UserId != userId)
    {
        return NotFound("Tracked task not found!");
    }
    return Ok(mapper.Map<TrackedTaskDto>(trackedTask));
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest.Get_TrackedTask_Another_User_Returns_NotFound_Test"`
Expected: PASS

- [ ] **Step 5: Run all tests**

Run: `dotnet test`
Expected: 49/49 pass

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server/Controllers/TrackedTaskController.cs Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs
git commit -m "fix: add ownership check to GetTrackedTask endpoint"
```

---

### Task 6: Add EndDate validation in UpdateTrackedTask

**Files:**
- Modify: `Timinute/Server/Controllers/TrackedTaskController.cs`
- Test: `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs`

- [ ] **Step 1: Write failing test**

Add to `TrackedTaskControllerTest.cs`:

```csharp
[Fact]
public async Task Update_TrackedTask_EndDate_Before_StartDate_Returns_BadRequest_Test()
{
    ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "EndDateTest");
    TrackedTaskController controller = await CreateController(applicationDbContext);

    var taskToUpdate = new UpdateTrackedTaskDto
    {
        TaskId = "TrackedTaskId1",
        Name = "Updated Task",
        StartDate = new DateTime(2021, 10, 1, 10, 0, 0),
        EndDate = new DateTime(2021, 10, 1, 8, 0, 0), // EndDate before StartDate
    };

    var actionResult = await controller.UpdateTrackedTask(taskToUpdate);

    Assert.NotNull(actionResult);
    Assert.IsAssignableFrom<BadRequestObjectResult>(actionResult.Result);

    var badRequestResult = actionResult.Result as BadRequestObjectResult;
    Assert.NotNull(badRequestResult);
    Assert.Equal("End date must be after start date.", badRequestResult!.Value);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest.Update_TrackedTask_EndDate_Before_StartDate_Returns_BadRequest_Test"`
Expected: FAIL — returns 200 OK instead of 400

- [ ] **Step 3: Add EndDate validation to UpdateTrackedTask**

In `TrackedTaskController.cs`, in the `UpdateTrackedTask` method, after the line `updatedTrackedTask.StartDate = updatedTrackedTask.StartDate.ToUniversalTime();` (line 158) and before the `if (updatedTrackedTask.EndDate.HasValue)` block (line 160), add the validation:

Replace lines 158-164:
```csharp
updatedTrackedTask.StartDate = updatedTrackedTask.StartDate.ToUniversalTime();

if (updatedTrackedTask.EndDate.HasValue)
{
    updatedTrackedTask.EndDate = updatedTrackedTask.EndDate.Value.ToUniversalTime();
    updatedTrackedTask.Duration = updatedTrackedTask.EndDate.Value - updatedTrackedTask.StartDate;
}
```

With:
```csharp
updatedTrackedTask.StartDate = updatedTrackedTask.StartDate.ToUniversalTime();

if (updatedTrackedTask.EndDate.HasValue)
{
    updatedTrackedTask.EndDate = updatedTrackedTask.EndDate.Value.ToUniversalTime();

    if (updatedTrackedTask.EndDate.Value < updatedTrackedTask.StartDate)
    {
        return BadRequest("End date must be after start date.");
    }

    updatedTrackedTask.Duration = updatedTrackedTask.EndDate.Value - updatedTrackedTask.StartDate;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest.Update_TrackedTask_EndDate_Before_StartDate_Returns_BadRequest_Test"`
Expected: PASS

- [ ] **Step 5: Run all tests**

Run: `dotnet test`
Expected: 50/50 pass

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server/Controllers/TrackedTaskController.cs Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs
git commit -m "feat: validate EndDate >= StartDate in UpdateTrackedTask"
```

---

### Task 7: Final verification

- [ ] **Step 1: Clean build**

Run: `dotnet clean Timinute.sln && dotnet build Timinute.sln`
Expected: 0 errors

- [ ] **Step 2: Run full test suite**

Run: `dotnet test --verbosity normal`
Expected: 50/50 pass

- [ ] **Step 3: Verify no untracked files left behind**

Run: `git status`
Expected: Clean working tree (no unstaged changes)
