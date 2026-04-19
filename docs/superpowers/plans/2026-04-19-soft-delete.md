# Soft Delete Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `DELETE` on Project and TrackedTask recoverable for 30 days via a `DeletedAt` timestamp + EF global query filter, with cascade on Project delete, a Trash page + undo toast in the Blazor client, and a background service that hard-purges expired rows.

**Architecture:** Add `ISoftDeletable` marker interface and `DeletedAt DateTimeOffset?` column to the two entities. EF global query filter hides deleted rows from all existing queries. Repository gains four methods (`SoftDelete`, `Restore`, `GetDeleted`, `PurgeExpired`); bypasses the filter via `IgnoreQueryFilters()`. Cascade logic lives in `ProjectController` (soft-delete children with matching timestamp, restore siblings by timestamp match). `TrashPurgeService : BackgroundService` runs daily. Client adds `/trash` page + Radzen `NotificationMessage` with click-to-undo.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core with SQL Server, System.Linq.Dynamic.Core (already referenced), xUnit + Moq + EF InMemory, Blazor WebAssembly, Radzen.Blazor.

**Reference:** spec at `docs/superpowers/specs/2026-04-19-soft-delete-design.md`.

---

## Phase 1: Data Model Foundation

### Task 1: Add `ISoftDeletable` marker interface

**Files:**
- Create: `Timinute/Server/Models/ISoftDeletable.cs`

- [ ] **Step 1: Create the interface**

```csharp
namespace Timinute.Server.Models
{
    public interface ISoftDeletable
    {
        DateTimeOffset? DeletedAt { get; set; }
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Timinute.sln`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Timinute/Server/Models/ISoftDeletable.cs
git commit -m "feat: add ISoftDeletable marker interface"
```

---

### Task 2: Add `DeletedAt` to `Project` and `TrackedTask`

**Files:**
- Modify: `Timinute/Server/Models/Project.cs`
- Modify: `Timinute/Server/Models/TrackedTask.cs`

- [ ] **Step 1: Update `Project.cs`**

Replace full file:

```csharp
namespace Timinute.Server.Models
{
    public class Project : ISoftDeletable
    {
        public string ProjectId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public ICollection<TrackedTask>? TrackedTasks { get; set; }
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
```

- [ ] **Step 2: Update `TrackedTask.cs`**

Replace full file:

```csharp
namespace Timinute.Server.Models
{
    public class TrackedTask : ISoftDeletable
    {
        public string TaskId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public TimeSpan Duration { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public string? ProjectId { get; set; }
        public Project? Project { get; set; }
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Timinute.sln`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Timinute/Server/Models/Project.cs Timinute/Server/Models/TrackedTask.cs
git commit -m "feat: add DeletedAt column to Project and TrackedTask"
```

---

### Task 3: Configure EF global query filter + index

**Files:**
- Modify: `Timinute/Server/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Add query filter and index in `OnModelCreating`**

In `ApplicationDbContext.cs`, inside `OnModelCreating`, after the existing `builder.Entity<Project>().HasKey(t => t.ProjectId);` line (line 28), add:

```csharp
            builder.Entity<Project>()
                .HasQueryFilter(p => p.DeletedAt == null);

            builder.Entity<Project>()
                .HasIndex(p => p.DeletedAt);
```

After `builder.Entity<TrackedTask>().HasKey(t => t.TaskId);` (line 47), add:

```csharp
            builder.Entity<TrackedTask>()
                .HasQueryFilter(t => t.DeletedAt == null);

            builder.Entity<TrackedTask>()
                .HasIndex(t => t.DeletedAt);
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Timinute.sln`
Expected: 0 errors. An EF warning about navigation filters across `Project→TrackedTask` may appear — add `builder.Entity<TrackedTask>().Ignore(...)` is NOT needed. If the warning `The entity type 'TrackedTask' has a defining navigation and the query filter...` appears, it can be suppressed by adding the same filter predicate on both sides (already done above).

- [ ] **Step 3: Commit**

```bash
git add Timinute/Server/Data/ApplicationDbContext.cs
git commit -m "feat: add EF global query filter and index for soft delete"
```

---

### Task 4: Create EF migration

**Files:**
- Create: `Timinute/Server/Data/Migrations/<timestamp>_AddSoftDelete.cs` (auto-generated)

- [ ] **Step 1: Generate migration**

Run:
```bash
dotnet ef migrations add AddSoftDelete --project Timinute/Server/Timinute.Server.csproj
```

Expected: creates two files under `Timinute/Server/Data/Migrations/` (the migration and its Designer).

- [ ] **Step 2: Inspect generated migration**

Open the new migration. It should contain `AddColumn` for `DeletedAt` on both `Projects` and `TrackedTasks` and `CreateIndex` on the same column for each. No data-loss operations. If any unexpected operation is present (e.g., drop/rename from stale model diff), investigate before proceeding — do NOT silently accept.

- [ ] **Step 3: Verify build**

Run: `dotnet build Timinute.sln`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Timinute/Server/Data/Migrations/
git commit -m "feat: add EF migration for soft delete columns and indexes"
```

---

## Phase 2: Repository Layer

### Task 5: Extend `IRepository<T>` with soft-delete methods

**Files:**
- Modify: `Timinute/Server/Repository/IRepository.cs`

- [ ] **Step 1: Add four new method signatures**

Replace full file:

```csharp
using System.Linq.Expressions;
using Timinute.Server.Models.Paging;

namespace Timinute.Server.Repository
{
    public interface IRepository<TEntity> where TEntity : class
    {
        Task Delete(TEntity entityToDelete);
        Task Delete(object id);
        Task<TEntity?> Find(object id);
        Task<PagedList<TEntity>> GetPaged(PagingParameters parameters, Expression<Func<TEntity, bool>>? filter = null,
            string orderBy = null, string includeProperties = "");
        Task<IEnumerable<TEntity>> Get(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, string includeProperties = "");
        Task<IEnumerable<TType>> Get<TType>(Expression<Func<TEntity, TType>> select, Expression<Func<TEntity, bool>>? where = null) where TType : class;
        Task<TEntity?> GetByIdInclude(Expression<Func<TEntity, bool>>? filter = null, string includeProperties = "");
        Task<TEntity?> GetById(object id);
        Task<IEnumerable<TEntity>> GetWithRawSql(string query, params object[] parameters);
        Task Insert(TEntity entity);
        Task Update(TEntity entityToUpdate);

        Task SoftDelete(object id);
        Task Restore(object id);
        Task<IEnumerable<TEntity>> GetDeleted(Expression<Func<TEntity, bool>>? filter = null);
        Task<int> PurgeExpired(DateTimeOffset olderThan);
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Timinute.sln`
Expected: compile error — `BaseRepository<TEntity>` does not implement the new members. This is expected; next task fixes it.

- [ ] **Step 3: Do NOT commit yet** — wait until `BaseRepository` implements the methods (next task).

---

### Task 6: Implement soft-delete methods in `BaseRepository<T>`

**Files:**
- Modify: `Timinute/Server/Repository/BaseRepository.cs`
- Test: `Timinute/Server.Tests/Repositories/ProjectRepositoryTest.cs`
- Test: `Timinute/Server.Tests/Repositories/TrackedTaskRepositoryTest.cs`

**Context:** Query filter bypass uses `IgnoreQueryFilters()`. Dynamic key name lookup uses `context.Model.FindEntityType(...)!.FindPrimaryKey()!.Properties[0].Name`. Dynamic LINQ (`System.Linq.Dynamic.Core`) is already referenced and allows string-based `Where("DeletedAt != null")`.

- [ ] **Step 1: Write failing repository test for `SoftDelete`**

Add to `ProjectRepositoryTest.cs` at the end of the class (before the closing brace):

```csharp
        [Fact]
        public async Task SoftDelete_Marks_Entity_And_Hides_From_Default_Query_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "SoftDelete");
            var repository = new BaseRepository<Project>(dbContext);

            await repository.SoftDelete("ProjectId4");

            var found = await repository.GetById("ProjectId4");
            Assert.Null(found);

            var stillInDb = await dbContext.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProjectId == "ProjectId4");
            Assert.NotNull(stillInDb);
            Assert.NotNull(stillInDb!.DeletedAt);
        }
```

Add `using Microsoft.EntityFrameworkCore;` at the top if not already present (it is).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ProjectRepositoryTest.SoftDelete_Marks_Entity_And_Hides_From_Default_Query_Test"`
Expected: FAIL — `SoftDelete` throws or compiles but does nothing.

- [ ] **Step 3: Implement `SoftDelete` in `BaseRepository<T>`**

In `BaseRepository.cs`, add the following four methods at the end of the class (before the closing brace):

```csharp
        public async Task SoftDelete(object id)
        {
            var entity = await dbSet.FindAsync(id);
            if (entity is ISoftDeletable softDeletable)
            {
                softDeletable.DeletedAt = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync();
            }
            else if (entity != null)
            {
                throw new InvalidOperationException(
                    $"Entity of type {typeof(TEntity).Name} does not implement ISoftDeletable.");
            }
        }

        public async Task Restore(object id)
        {
            var keyProperty = context.Model.FindEntityType(typeof(TEntity))!
                .FindPrimaryKey()!.Properties[0].Name;

            var entity = await dbSet.IgnoreQueryFilters()
                .Where($"{keyProperty} == @0", id)
                .FirstOrDefaultAsync();

            if (entity is ISoftDeletable softDeletable)
            {
                softDeletable.DeletedAt = null;
                await context.SaveChangesAsync();
            }
            else if (entity != null)
            {
                throw new InvalidOperationException(
                    $"Entity of type {typeof(TEntity).Name} does not implement ISoftDeletable.");
            }
        }

        public async Task<IEnumerable<TEntity>> GetDeleted(Expression<Func<TEntity, bool>>? filter = null)
        {
            IQueryable<TEntity> query = dbSet.IgnoreQueryFilters().Where("DeletedAt != null");

            if (filter != null)
            {
                query = query.Where(filter);
            }

            return await query.ToListAsync();
        }

        public async Task<int> PurgeExpired(DateTimeOffset olderThan)
        {
            return await dbSet.IgnoreQueryFilters()
                .Where("DeletedAt != null && DeletedAt < @0", olderThan)
                .ExecuteDeleteAsync();
        }
```

Add `using Timinute.Server.Models;` at the top of `BaseRepository.cs` (needed for `ISoftDeletable`).

- [ ] **Step 4: Run SoftDelete test to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ProjectRepositoryTest.SoftDelete_Marks_Entity_And_Hides_From_Default_Query_Test"`
Expected: PASS.

- [ ] **Step 5: Write failing test for `Restore`**

Add to `ProjectRepositoryTest.cs`:

```csharp
        [Fact]
        public async Task Restore_Clears_DeletedAt_And_Restores_To_Default_Query_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "Restore");
            var repository = new BaseRepository<Project>(dbContext);

            await repository.SoftDelete("ProjectId5");
            Assert.Null(await repository.GetById("ProjectId5"));

            await repository.Restore("ProjectId5");

            var restored = await repository.GetById("ProjectId5");
            Assert.NotNull(restored);
            Assert.Null(restored!.DeletedAt);
        }
```

- [ ] **Step 6: Run test to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ProjectRepositoryTest.Restore_Clears_DeletedAt_And_Restores_To_Default_Query_Test"`
Expected: PASS.

- [ ] **Step 7: Write failing test for `GetDeleted`**

Add to `ProjectRepositoryTest.cs`:

```csharp
        [Fact]
        public async Task GetDeleted_Returns_Only_SoftDeleted_Entities_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "GetDeleted");
            var repository = new BaseRepository<Project>(dbContext);

            await repository.SoftDelete("ProjectId4");
            await repository.SoftDelete("ProjectId5");

            var deleted = (await repository.GetDeleted()).ToList();

            Assert.Equal(2, deleted.Count);
            Assert.All(deleted, p => Assert.NotNull(p.DeletedAt));
            Assert.Contains(deleted, p => p.ProjectId == "ProjectId4");
            Assert.Contains(deleted, p => p.ProjectId == "ProjectId5");
        }
```

- [ ] **Step 8: Run test to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ProjectRepositoryTest.GetDeleted_Returns_Only_SoftDeleted_Entities_Test"`
Expected: PASS.

- [ ] **Step 9: Write failing test for `PurgeExpired`**

Add to `ProjectRepositoryTest.cs`:

```csharp
        [Fact]
        public async Task PurgeExpired_Removes_Old_SoftDeleted_Only_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "Purge");
            var repository = new BaseRepository<Project>(dbContext);

            // Soft-delete two projects, then manually age one of them by 40 days.
            await repository.SoftDelete("ProjectId4");
            await repository.SoftDelete("ProjectId5");

            var aged = await dbContext.Projects.IgnoreQueryFilters().FirstAsync(p => p.ProjectId == "ProjectId4");
            aged.DeletedAt = DateTimeOffset.UtcNow.AddDays(-40);
            await dbContext.SaveChangesAsync();

            var purgedCount = await repository.PurgeExpired(DateTimeOffset.UtcNow.AddDays(-30));

            Assert.Equal(1, purgedCount);

            var remaining = await dbContext.Projects.IgnoreQueryFilters().Where(p => p.ProjectId == "ProjectId4" || p.ProjectId == "ProjectId5").ToListAsync();
            Assert.Single(remaining);
            Assert.Equal("ProjectId5", remaining[0].ProjectId);
        }
```

- [ ] **Step 10: Run test to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ProjectRepositoryTest.PurgeExpired_Removes_Old_SoftDeleted_Only_Test"`
Expected: PASS.

- [ ] **Step 11: Repeat analogous four tests for `TrackedTaskRepositoryTest.cs`**

Add to `TrackedTaskRepositoryTest.cs` (using `TrackedTaskId4`/`TrackedTaskId5` which belong to `ApplicationUser1`/non-project task — any unused ID works; use `TrackedTaskId3` and `TrackedTaskId4` which belong to `ApplicationUser1`):

```csharp
        [Fact]
        public async Task SoftDelete_Marks_Entity_And_Hides_From_Default_Query_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "SoftDelete");
            var repository = new BaseRepository<TrackedTask>(dbContext);

            await repository.SoftDelete("TrackedTaskId3");

            var found = await repository.GetById("TrackedTaskId3");
            Assert.Null(found);

            var stillInDb = await dbContext.TrackedTasks.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId3");
            Assert.NotNull(stillInDb);
            Assert.NotNull(stillInDb!.DeletedAt);
        }

        [Fact]
        public async Task Restore_Clears_DeletedAt_And_Restores_To_Default_Query_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "Restore");
            var repository = new BaseRepository<TrackedTask>(dbContext);

            await repository.SoftDelete("TrackedTaskId4");
            Assert.Null(await repository.GetById("TrackedTaskId4"));

            await repository.Restore("TrackedTaskId4");

            var restored = await repository.GetById("TrackedTaskId4");
            Assert.NotNull(restored);
            Assert.Null(restored!.DeletedAt);
        }

        [Fact]
        public async Task GetDeleted_Returns_Only_SoftDeleted_Entities_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "GetDeleted");
            var repository = new BaseRepository<TrackedTask>(dbContext);

            await repository.SoftDelete("TrackedTaskId3");
            await repository.SoftDelete("TrackedTaskId4");

            var deleted = (await repository.GetDeleted()).ToList();

            Assert.Equal(2, deleted.Count);
            Assert.All(deleted, t => Assert.NotNull(t.DeletedAt));
        }

        [Fact]
        public async Task PurgeExpired_Removes_Old_SoftDeleted_Only_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(dbName + "Purge");
            var repository = new BaseRepository<TrackedTask>(dbContext);

            await repository.SoftDelete("TrackedTaskId3");
            await repository.SoftDelete("TrackedTaskId4");

            var aged = await dbContext.TrackedTasks.IgnoreQueryFilters().FirstAsync(t => t.TaskId == "TrackedTaskId3");
            aged.DeletedAt = DateTimeOffset.UtcNow.AddDays(-40);
            await dbContext.SaveChangesAsync();

            var purgedCount = await repository.PurgeExpired(DateTimeOffset.UtcNow.AddDays(-30));

            Assert.Equal(1, purgedCount);
        }
```

Add `using Microsoft.EntityFrameworkCore;` at the top if not present.

- [ ] **Step 12: Run all repository tests**

Run: `dotnet test --filter "FullyQualifiedName~Repositories"`
Expected: all pass (new + existing).

- [ ] **Step 13: Run full test suite to ensure no regressions**

Run: `dotnet test`
Expected: all existing tests still pass (query filter is transparent for seeded active rows).

- [ ] **Step 14: Commit**

```bash
git add Timinute/Server/Repository/IRepository.cs \
        Timinute/Server/Repository/BaseRepository.cs \
        Timinute/Server.Tests/Repositories/ProjectRepositoryTest.cs \
        Timinute/Server.Tests/Repositories/TrackedTaskRepositoryTest.cs
git commit -m "feat: add soft-delete, restore, get-deleted, purge-expired to repository"
```

---

## Phase 3: Shared DTOs

### Task 7: Add `TrashItemDto`

**Files:**
- Create: `Timinute/Shared/Dtos/Trash/TrashItemDto.cs`

- [ ] **Step 1: Create the DTO**

```csharp
namespace Timinute.Shared.Dtos.Trash
{
    public class TrashItemDto
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public DateTimeOffset DeletedAt { get; set; }
        public int DaysRemaining { get; set; }
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Timinute.sln`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Timinute/Shared/Dtos/Trash/TrashItemDto.cs
git commit -m "feat: add TrashItemDto for trash listing endpoints"
```

---

## Phase 4: TrackedTask Controller — soft-delete endpoints

### Task 8: Change `DeleteTrackedTask` to soft-delete

**Files:**
- Modify: `Timinute/Server/Controllers/TrackedTaskController.cs`
- Test: `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs`

- [ ] **Step 1: Write failing test**

Add to `TrackedTaskControllerTest.cs` before the `CreateController` method (the private method that builds the controller):

```csharp
        [Fact]
        public async Task Delete_TrackedTask_Soft_Deletes_Row_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "SoftDeleteTask");
            TrackedTaskController controller = await CreateController(applicationDbContext);

            var actionResult = await controller.DeleteTrackedTask("TrackedTaskId1");

            Assert.IsType<NoContentResult>(actionResult);

            // Row still exists in DB but with DeletedAt set, and is hidden from default queries.
            var stillInDb = await applicationDbContext.TrackedTasks.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId1");
            Assert.NotNull(stillInDb);
            Assert.NotNull(stillInDb!.DeletedAt);

            var hidden = await applicationDbContext.TrackedTasks.FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId1");
            Assert.Null(hidden);
        }
```

Add `using Microsoft.EntityFrameworkCore;` at the top of the test file if not present.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest.Delete_TrackedTask_Soft_Deletes_Row_Test"`
Expected: FAIL — delete currently hard-removes row, so `stillInDb` is null.

- [ ] **Step 3: Update `DeleteTrackedTask` to use `SoftDelete`**

In `TrackedTaskController.cs`, replace the `DeleteTrackedTask` method (lines 145-173) with:

```csharp
        // DELETE: api/TrackedTask
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTrackedTask(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var trackedTaskToDelete = await taskRepository.Find(id);
            if (trackedTaskToDelete == null)
            {
                logger.LogError("Tracked task was not found");
                return NotFound("Tracked task not found!");
            }

            if (trackedTaskToDelete.UserId != userId)
            {
                return NotFound("Tracked task not found!");
            }

            await taskRepository.SoftDelete(id);

            logger.LogInformation($"Tracked task with Id {id} was soft-deleted.");
            return NoContent();
        }
```

- [ ] **Step 4: Run test to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest.Delete_TrackedTask_Soft_Deletes_Row_Test"`
Expected: PASS.

- [ ] **Step 5: Run all TrackedTask controller tests**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest"`
Expected: all pass. Existing `Delete_Existing_TrackedTask_Test` should still pass (it asserts `NoContentResult`, not row removal).

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server/Controllers/TrackedTaskController.cs \
        Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs
git commit -m "feat: change DeleteTrackedTask to soft-delete"
```

---

### Task 9: Add `RestoreTrackedTask` endpoint

**Files:**
- Modify: `Timinute/Server/Controllers/TrackedTaskController.cs`
- Test: `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs`

- [ ] **Step 1: Write failing test**

Add to `TrackedTaskControllerTest.cs`:

```csharp
        [Fact]
        public async Task Restore_TrackedTask_Returns_Task_To_Default_Query_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "RestoreTask");
            TrackedTaskController controller = await CreateController(applicationDbContext);

            await controller.DeleteTrackedTask("TrackedTaskId1");

            var actionResult = await controller.RestoreTrackedTask("TrackedTaskId1");
            Assert.IsType<NoContentResult>(actionResult);

            var restored = await applicationDbContext.TrackedTasks.FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId1");
            Assert.NotNull(restored);
            Assert.Null(restored!.DeletedAt);
        }

        [Fact]
        public async Task Restore_TrackedTask_Another_User_Returns_NotFound_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "RestoreTaskAuth");
            TrackedTaskController owner = await CreateController(applicationDbContext);
            await owner.DeleteTrackedTask("TrackedTaskId1");

            TrackedTaskController other = await CreateController(applicationDbContext, "ApplicationUser10");
            var actionResult = await other.RestoreTrackedTask("TrackedTaskId1");

            Assert.IsType<NotFoundObjectResult>(actionResult);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest.Restore_TrackedTask"`
Expected: FAIL — `RestoreTrackedTask` doesn't exist.

- [ ] **Step 3: Add `RestoreTrackedTask`**

In `TrackedTaskController.cs`, add the following method after `DeleteTrackedTask`:

```csharp
        // RESTORE: api/TrackedTask/{id}/restore
        [HttpPost("{id}/restore")]
        public async Task<ActionResult> RestoreTrackedTask(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var deleted = (await taskRepository.GetDeleted(t => t.TaskId == id && t.UserId == userId)).FirstOrDefault();
            if (deleted == null)
            {
                return NotFound("Tracked task not found!");
            }

            await taskRepository.Restore(id);

            logger.LogInformation($"Tracked task with Id {id} was restored.");
            return NoContent();
        }
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest.Restore_TrackedTask"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Timinute/Server/Controllers/TrackedTaskController.cs \
        Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs
git commit -m "feat: add RestoreTrackedTask endpoint"
```

---

### Task 10: Add `GetTrashTrackedTasks` endpoint

**Files:**
- Modify: `Timinute/Server/Controllers/TrackedTaskController.cs`
- Test: `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs`

**Retention constant note:** the trash endpoint needs to know the retention window to compute `DaysRemaining`. Read from `IConfiguration` (`TrashRetention:Days`, default 30). The full config section is added in Task 15; here just inject `IConfiguration` and use it.

- [ ] **Step 1: Write failing test**

Add to `TrackedTaskControllerTest.cs`:

```csharp
        [Fact]
        public async Task GetTrash_TrackedTasks_Returns_Only_Deleted_Owned_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "TrashTask");
            TrackedTaskController controller = await CreateController(applicationDbContext);

            await controller.DeleteTrackedTask("TrackedTaskId1");
            await controller.DeleteTrackedTask("TrackedTaskId2");

            var actionResult = await controller.GetTrashTrackedTasks();

            Assert.NotNull(actionResult);
            var okResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okResult);
            var items = okResult!.Value as IEnumerable<TrashItemDto>;
            Assert.NotNull(items);
            var list = items!.ToList();
            Assert.Equal(2, list.Count);
            Assert.All(list, i => Assert.InRange(i.DaysRemaining, 29, 30));
        }
```

Add `using Timinute.Shared.Dtos.Trash;` at the top of the test file.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest.GetTrash_TrackedTasks_Returns_Only_Deleted_Owned_Test"`
Expected: FAIL — method doesn't exist.

- [ ] **Step 3: Inject `IConfiguration` into controller constructor**

In `TrackedTaskController.cs`, update the class:

```csharp
        private readonly IRepository<TrackedTask> taskRepository;
        private readonly IMapper mapper;
        private readonly ILogger<TrackedTaskController> logger;
        private readonly IConfiguration configuration;

        public TrackedTaskController(IRepositoryFactory repositoryFactory, IMapper mapper, ILogger<TrackedTaskController> logger, IConfiguration configuration)
        {
            this.mapper = mapper;
            this.logger = logger;
            this.configuration = configuration;

            taskRepository = repositoryFactory.GetRepository<TrackedTask>();
        }
```

Update all `CreateController` methods in `TrackedTaskControllerTest.cs` (search the file) to pass a real `IConfiguration`. In the test's `CreateController`:

```csharp
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["TrashRetention:Days"] = "30" })
                .Build();

            TrackedTaskController controller = new(repositoryFactory, _mapper, _loggerMock.Object, configuration)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                }
            };
```

Add `using Microsoft.Extensions.Configuration;` at the top of the test file if not present.

- [ ] **Step 4: Add `GetTrashTrackedTasks` method**

In `TrackedTaskController.cs`, after `RestoreTrackedTask`:

```csharp
        // GET: api/TrackedTask/trash
        [HttpGet("trash")]
        public async Task<ActionResult<IEnumerable<TrashItemDto>>> GetTrashTrackedTasks()
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var retentionDays = configuration.GetValue<int>("TrashRetention:Days", 30);
            var now = DateTimeOffset.UtcNow;

            var deleted = await taskRepository.GetDeleted(t => t.UserId == userId);

            var items = deleted.Select(t => new TrashItemDto
            {
                Id = t.TaskId,
                Name = t.Name,
                DeletedAt = t.DeletedAt!.Value,
                DaysRemaining = Math.Max(0, (int)Math.Ceiling(retentionDays - (now - t.DeletedAt.Value).TotalDays))
            });

            return Ok(items);
        }
```

Add `using Timinute.Shared.Dtos.Trash;` at the top of the controller.

- [ ] **Step 5: Run test to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest.GetTrash_TrackedTasks_Returns_Only_Deleted_Owned_Test"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server/Controllers/TrackedTaskController.cs \
        Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs
git commit -m "feat: add GetTrashTrackedTasks endpoint"
```

---

### Task 11: Add `PurgeTrackedTask` endpoint

**Files:**
- Modify: `Timinute/Server/Controllers/TrackedTaskController.cs`
- Test: `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs`

- [ ] **Step 1: Write failing test**

Add to `TrackedTaskControllerTest.cs`:

```csharp
        [Fact]
        public async Task Purge_TrackedTask_Hard_Removes_Row_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "PurgeTask");
            TrackedTaskController controller = await CreateController(applicationDbContext);

            await controller.DeleteTrackedTask("TrackedTaskId1");

            var actionResult = await controller.PurgeTrackedTask("TrackedTaskId1");
            Assert.IsType<NoContentResult>(actionResult);

            var row = await applicationDbContext.TrackedTasks.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId1");
            Assert.Null(row);
        }

        [Fact]
        public async Task Purge_TrackedTask_Another_User_Returns_NotFound_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "PurgeTaskAuth");
            TrackedTaskController owner = await CreateController(applicationDbContext);
            await owner.DeleteTrackedTask("TrackedTaskId1");

            TrackedTaskController other = await CreateController(applicationDbContext, "ApplicationUser10");
            var actionResult = await other.PurgeTrackedTask("TrackedTaskId1");

            Assert.IsType<NotFoundObjectResult>(actionResult);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest.Purge_TrackedTask"`
Expected: FAIL — method doesn't exist.

- [ ] **Step 3: Add `PurgeTrackedTask`**

In `TrackedTaskController.cs`, after `GetTrashTrackedTasks`:

```csharp
        // PURGE: api/TrackedTask/{id}/purge
        [HttpDelete("{id}/purge")]
        public async Task<ActionResult> PurgeTrackedTask(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var deleted = (await taskRepository.GetDeleted(t => t.TaskId == id && t.UserId == userId)).FirstOrDefault();
            if (deleted == null)
            {
                return NotFound("Tracked task not found!");
            }

            await taskRepository.Delete(id);

            logger.LogInformation($"Tracked task with Id {id} was purged.");
            return NoContent();
        }
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~TrackedTaskControllerTest.Purge_TrackedTask"`
Expected: PASS.

- [ ] **Step 5: Run full test suite**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server/Controllers/TrackedTaskController.cs \
        Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs
git commit -m "feat: add PurgeTrackedTask endpoint"
```

---

## Phase 5: Project Controller — soft-delete endpoints with cascade

### Task 12: Change `DeleteProject` to soft-delete with task cascade

**Files:**
- Modify: `Timinute/Server/Controllers/ProjectController.cs`
- Test: `Timinute/Server.Tests/Controllers/ProjectControllerTest.cs`

**Implementation note:** the cascade runs in the controller because the repository is generic and doesn't know about the `Project→TrackedTask` relationship. We access both repositories via the injected `IRepositoryFactory`. To set the same timestamp on parent + children, we compute `var deletedAt = DateTimeOffset.UtcNow` once, then manually set it on each entity and call `Update` on both (or use the DbContext directly via a new repository method — keep it simple and do it via controller-level `Update` calls).

- [ ] **Step 1: Inject `TrackedTask` repository into `ProjectController`**

In `ProjectController.cs`, update the class to hold both repositories and `IConfiguration`:

```csharp
        private readonly IRepository<Project> projectRepository;
        private readonly IRepository<TrackedTask> taskRepository;
        private readonly IMapper mapper;
        private readonly ILogger<ProjectController> logger;
        private readonly IConfiguration configuration;

        public ProjectController(IRepositoryFactory repositoryFactory, IMapper mapper, ILogger<ProjectController> logger, IConfiguration configuration)
        {
            this.mapper = mapper;
            this.logger = logger;
            this.configuration = configuration;

            projectRepository = repositoryFactory.GetRepository<Project>();
            taskRepository = repositoryFactory.GetRepository<TrackedTask>();
        }
```

Update the `CreateController` method at the bottom of `ProjectControllerTest.cs` to build and pass an `IConfiguration`:

```csharp
        protected override async Task<ProjectController> CreateController(ApplicationDbContext? applicationDbContext = null, string userId = "ApplicationUser1")
        {
            if (applicationDbContext == null)
            {
                applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName);
            }

            var repositoryFactory = new RepositoryFactory(applicationDbContext);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                                        new Claim("sub", userId),
                                        new Claim(ClaimTypes.Name, "test1@email.com")
                                        }
            ));

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["TrashRetention:Days"] = "30" })
                .Build();

            ProjectController controller = new(repositoryFactory, _mapper, _loggerMock.Object, configuration)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                }
            };

            return controller;
        }
```

Add `using Microsoft.Extensions.Configuration;` at the top of the test file if not present.

- [ ] **Step 2: Write failing test**

Add to `ProjectControllerTest.cs`:

```csharp
        [Fact]
        public async Task Delete_Project_Soft_Deletes_Project_And_Cascades_To_Tasks_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CascadeDelete");
            ProjectController controller = await CreateController(applicationDbContext);

            // ProjectId1 owns TrackedTaskId1, 2, 3 (all active, owned by ApplicationUser1)
            var actionResult = await controller.DeleteProject("ProjectId1");
            Assert.IsType<NoContentResult>(actionResult);

            var project = await applicationDbContext.Projects.IgnoreQueryFilters().FirstAsync(p => p.ProjectId == "ProjectId1");
            Assert.NotNull(project.DeletedAt);

            var tasks = await applicationDbContext.TrackedTasks.IgnoreQueryFilters()
                .Where(t => t.ProjectId == "ProjectId1")
                .ToListAsync();

            Assert.Equal(3, tasks.Count);
            Assert.All(tasks, t => Assert.Equal(project.DeletedAt, t.DeletedAt));
        }
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ProjectControllerTest.Delete_Project_Soft_Deletes_Project_And_Cascades_To_Tasks_Test"`
Expected: FAIL — current implementation hard-deletes with FK `SetNull`.

- [ ] **Step 4: Replace `DeleteProject`**

In `ProjectController.cs`, replace the `DeleteProject` method (lines 138-165) with:

```csharp
        // DELETE: api/Project
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteProject(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var projectToDelete = await projectRepository.GetById(id);
            if (projectToDelete == null)
            {
                logger.LogError("Project was not found");
                return NotFound("Project not found!");
            }

            if (projectToDelete.UserId != userId)
            {
                return NotFound("Project not found!");
            }

            var deletedAt = DateTimeOffset.UtcNow;

            // Soft-delete active child tasks with the same timestamp for cascade-restore matching.
            var activeChildTasks = await taskRepository.Get(t => t.ProjectId == id);
            foreach (var task in activeChildTasks)
            {
                task.DeletedAt = deletedAt;
                await taskRepository.Update(task);
            }

            projectToDelete.DeletedAt = deletedAt;
            await projectRepository.Update(projectToDelete);

            logger.LogInformation($"Project with Id {projectToDelete.ProjectId} soft-deleted along with {activeChildTasks.Count()} tasks.");
            return NoContent();
        }
```

- [ ] **Step 5: Run test to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ProjectControllerTest.Delete_Project_Soft_Deletes_Project_And_Cascades_To_Tasks_Test"`
Expected: PASS.

- [ ] **Step 6: Run all ProjectController tests**

Run: `dotnet test --filter "FullyQualifiedName~ProjectControllerTest"`
Expected: all pass. The existing `Delete_Existing_Project_Test` asserts `NoContentResult`; still valid.

- [ ] **Step 7: Commit**

```bash
git add Timinute/Server/Controllers/ProjectController.cs \
        Timinute/Server.Tests/Controllers/ProjectControllerTest.cs
git commit -m "feat: change DeleteProject to soft-delete with task cascade"
```

---

### Task 13: Add `RestoreProject` endpoint with sibling-task restore

**Files:**
- Modify: `Timinute/Server/Controllers/ProjectController.cs`
- Test: `Timinute/Server.Tests/Controllers/ProjectControllerTest.cs`

- [ ] **Step 1: Write failing test (restore cascade)**

Add to `ProjectControllerTest.cs`:

```csharp
        [Fact]
        public async Task Restore_Project_Restores_Cascaded_Tasks_Only_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CascadeRestore");
            ProjectController controller = await CreateController(applicationDbContext);

            // Create a TrackedTask controller so we can delete one task individually before the project.
            var taskConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["TrashRetention:Days"] = "30" })
                .Build();
            var taskController = new TrackedTaskController(
                new RepositoryFactory(applicationDbContext),
                _mapper,
                new Mock<ILogger<TrackedTaskController>>().Object,
                taskConfig)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "ApplicationUser1") }))
                    }
                }
            };

            // Step 1: individually delete TrackedTaskId1 BEFORE project delete.
            await taskController.DeleteTrackedTask("TrackedTaskId1");

            // Step 2: delete the project (cascades to still-active TrackedTaskId2, 3).
            await controller.DeleteProject("ProjectId1");

            // Step 3: restore the project.
            var actionResult = await controller.RestoreProject("ProjectId1");
            Assert.IsType<NoContentResult>(actionResult);

            // TrackedTaskId2 and TrackedTaskId3 should be restored (they matched the project timestamp).
            var task2 = await applicationDbContext.TrackedTasks.FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId2");
            var task3 = await applicationDbContext.TrackedTasks.FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId3");
            Assert.NotNull(task2);
            Assert.NotNull(task3);

            // TrackedTaskId1 was deleted individually — should STAY deleted.
            var task1 = await applicationDbContext.TrackedTasks.IgnoreQueryFilters().First(t => t.TaskId == "TrackedTaskId1");
            Assert.NotNull(task1.DeletedAt);

            // The project itself is restored.
            var project = await applicationDbContext.Projects.FirstOrDefaultAsync(p => p.ProjectId == "ProjectId1");
            Assert.NotNull(project);
        }
```

Add `using Moq;` and `using Timinute.Server.Controllers;` to the top of the test file if not present.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ProjectControllerTest.Restore_Project_Restores_Cascaded_Tasks_Only_Test"`
Expected: FAIL — `RestoreProject` doesn't exist.

- [ ] **Step 3: Add `RestoreProject`**

In `ProjectController.cs`, after `DeleteProject`:

```csharp
        // RESTORE: api/Project/{id}/restore
        [HttpPost("{id}/restore")]
        public async Task<ActionResult> RestoreProject(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var deleted = (await projectRepository.GetDeleted(p => p.ProjectId == id && p.UserId == userId)).FirstOrDefault();
            if (deleted == null)
            {
                return NotFound("Project not found!");
            }

            var projectDeletedAt = deleted.DeletedAt!.Value;

            // Restore child tasks whose DeletedAt matches the project's (cascaded together).
            var siblingTasks = await taskRepository.GetDeleted(t => t.ProjectId == id && t.DeletedAt == projectDeletedAt);
            foreach (var task in siblingTasks)
            {
                task.DeletedAt = null;
                await taskRepository.Update(task);
            }

            await projectRepository.Restore(id);

            logger.LogInformation($"Project with Id {id} restored along with {siblingTasks.Count()} tasks.");
            return NoContent();
        }
```

- [ ] **Step 4: Run test to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ProjectControllerTest.Restore_Project"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Timinute/Server/Controllers/ProjectController.cs \
        Timinute/Server.Tests/Controllers/ProjectControllerTest.cs
git commit -m "feat: add RestoreProject with cascade sibling-task restore"
```

---

### Task 14: Add `GetTrashProjects` and `PurgeProject` endpoints

**Files:**
- Modify: `Timinute/Server/Controllers/ProjectController.cs`
- Test: `Timinute/Server.Tests/Controllers/ProjectControllerTest.cs`

- [ ] **Step 1: Write failing tests**

Add to `ProjectControllerTest.cs`:

```csharp
        [Fact]
        public async Task GetTrash_Projects_Returns_Only_Deleted_Owned_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "TrashProjects");
            ProjectController controller = await CreateController(applicationDbContext);

            await controller.DeleteProject("ProjectId4");
            await controller.DeleteProject("ProjectId5");

            var actionResult = await controller.GetTrashProjects();
            var okResult = actionResult.Result as OkObjectResult;
            var items = (okResult!.Value as IEnumerable<TrashItemDto>)!.ToList();

            Assert.Equal(2, items.Count);
            Assert.All(items, i => Assert.InRange(i.DaysRemaining, 29, 30));
        }

        [Fact]
        public async Task Purge_Project_Hard_Removes_Project_Row_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "PurgeProject");
            ProjectController controller = await CreateController(applicationDbContext);

            await controller.DeleteProject("ProjectId1");

            var actionResult = await controller.PurgeProject("ProjectId1");
            Assert.IsType<NoContentResult>(actionResult);

            var project = await applicationDbContext.Projects.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.ProjectId == "ProjectId1");
            Assert.Null(project);

            // Child tasks survive the project purge (verified in SQL Server via FK OnDelete(SetNull);
            // EF InMemory provider doesn't enforce FK actions, so we only assert task rows still exist).
            var taskCount = await applicationDbContext.TrackedTasks.IgnoreQueryFilters()
                .CountAsync(t => t.TaskId == "TrackedTaskId1" || t.TaskId == "TrackedTaskId2" || t.TaskId == "TrackedTaskId3");
            Assert.Equal(3, taskCount);
        }

        [Fact]
        public async Task Purge_Project_Another_User_Returns_NotFound_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "PurgeProjectAuth");
            ProjectController owner = await CreateController(applicationDbContext);
            await owner.DeleteProject("ProjectId1");

            ProjectController other = await CreateController(applicationDbContext, "ApplicationUser10");
            var actionResult = await other.PurgeProject("ProjectId1");

            Assert.IsType<NotFoundObjectResult>(actionResult);
        }
```

Add `using Timinute.Shared.Dtos.Trash;` at the top.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ProjectControllerTest.GetTrash_Projects_Returns_Only_Deleted_Owned_Test|FullyQualifiedName~ProjectControllerTest.Purge_Project"`
Expected: FAIL — methods don't exist.

- [ ] **Step 3: Add endpoints**

In `ProjectController.cs`, after `RestoreProject`:

```csharp
        // GET: api/Project/trash
        [HttpGet("trash")]
        public async Task<ActionResult<IEnumerable<TrashItemDto>>> GetTrashProjects()
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var retentionDays = configuration.GetValue<int>("TrashRetention:Days", 30);
            var now = DateTimeOffset.UtcNow;

            var deleted = await projectRepository.GetDeleted(p => p.UserId == userId);

            var items = deleted.Select(p => new TrashItemDto
            {
                Id = p.ProjectId,
                Name = p.Name,
                DeletedAt = p.DeletedAt!.Value,
                DaysRemaining = Math.Max(0, (int)Math.Ceiling(retentionDays - (now - p.DeletedAt.Value).TotalDays))
            });

            return Ok(items);
        }

        // PURGE: api/Project/{id}/purge
        [HttpDelete("{id}/purge")]
        public async Task<ActionResult> PurgeProject(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var deleted = (await projectRepository.GetDeleted(p => p.ProjectId == id && p.UserId == userId)).FirstOrDefault();
            if (deleted == null)
            {
                return NotFound("Project not found!");
            }

            await projectRepository.Delete(id);

            logger.LogInformation($"Project with Id {id} was purged.");
            return NoContent();
        }
```

Add `using Timinute.Shared.Dtos.Trash;` at the top of the controller if not already present.

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ProjectControllerTest"`
Expected: all pass.

- [ ] **Step 5: Full regression**

Run: `dotnet test`
Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server/Controllers/ProjectController.cs \
        Timinute/Server.Tests/Controllers/ProjectControllerTest.cs
git commit -m "feat: add GetTrashProjects and PurgeProject endpoints"
```

---

## Phase 6: Background Purge Service

### Task 15: Add `TrashRetention` config keys

**Files:**
- Modify: `Timinute/Server/appsettings.json`

- [ ] **Step 1: Add config section**

Replace the full `appsettings.json` contents:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1,44555;Database=Timinute;User Id=sa;Password=TiminuteAdmin.;MultipleActiveResultSets=true;TrustServerCertificate=Yes"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "IdentityServer": {
    "Authority": "https://localhost:7047"
  },
  "TrashRetention": {
    "Days": 30,
    "PurgeIntervalHours": 24
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Timinute.sln`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Timinute/Server/appsettings.json
git commit -m "feat: add TrashRetention config section"
```

---

### Task 16: Create `TrashPurgeService`

**Files:**
- Create: `Timinute/Server/Services/TrashPurgeService.cs`
- Test: `Timinute/Server.Tests/Services/TrashPurgeServiceTest.cs`

- [ ] **Step 1: Write failing test**

Create `Timinute/Server.Tests/Services/TrashPurgeServiceTest.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Server.Services;
using Timinute.Server.Tests.Helpers;
using Xunit;

namespace Timinute.Server.Tests.Services
{
    public class TrashPurgeServiceTest
    {
        [Fact]
        public async Task RunOnce_Purges_Expired_Tasks_And_Projects_Only_Test()
        {
            var dbContext = await TestHelper.GetDefaultApplicationDbContext("TrashPurgeService_DB");
            var services = new ServiceCollection();
            services.AddSingleton(dbContext);
            services.AddTransient<IRepositoryFactory>(sp => new RepositoryFactory(sp.GetRequiredService<ApplicationDbContext>()));
            var provider = services.BuildServiceProvider();

            // Soft-delete two tasks, age one past 30 days. Same for one project.
            var repoFactory = provider.GetRequiredService<IRepositoryFactory>();
            var taskRepo = repoFactory.GetRepository<TrackedTask>();
            var projRepo = repoFactory.GetRepository<Project>();

            await taskRepo.SoftDelete("TrackedTaskId1");
            await taskRepo.SoftDelete("TrackedTaskId2");
            await projRepo.SoftDelete("ProjectId4");
            await projRepo.SoftDelete("ProjectId5");

            var oldTask = await dbContext.TrackedTasks.IgnoreQueryFilters().FirstAsync(t => t.TaskId == "TrackedTaskId1");
            oldTask.DeletedAt = DateTimeOffset.UtcNow.AddDays(-40);
            var oldProject = await dbContext.Projects.IgnoreQueryFilters().FirstAsync(p => p.ProjectId == "ProjectId4");
            oldProject.DeletedAt = DateTimeOffset.UtcNow.AddDays(-40);
            await dbContext.SaveChangesAsync();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> {
                    ["TrashRetention:Days"] = "30",
                    ["TrashRetention:PurgeIntervalHours"] = "24"
                })
                .Build();

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var scopeMock = new Mock<IServiceScope>();
            scopeMock.SetupGet(s => s.ServiceProvider).Returns(provider);
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var logger = new Mock<ILogger<TrashPurgeService>>();
            var service = new TrashPurgeService(scopeFactoryMock.Object, configuration, logger.Object);

            await service.RunOnce(CancellationToken.None);

            Assert.Null(await dbContext.TrackedTasks.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId1"));
            Assert.NotNull(await dbContext.TrackedTasks.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId2"));
            Assert.Null(await dbContext.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProjectId == "ProjectId4"));
            Assert.NotNull(await dbContext.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProjectId == "ProjectId5"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~TrashPurgeServiceTest"`
Expected: FAIL — `TrashPurgeService` does not exist.

- [ ] **Step 3: Create `TrashPurgeService`**

Create `Timinute/Server/Services/TrashPurgeService.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Timinute.Server.Models;
using Timinute.Server.Repository;

namespace Timinute.Server.Services
{
    public class TrashPurgeService : BackgroundService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IConfiguration configuration;
        private readonly ILogger<TrashPurgeService> logger;

        public TrashPurgeService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<TrashPurgeService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.configuration = configuration;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalHours = configuration.GetValue<int>("TrashRetention:PurgeIntervalHours", 24);
            var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

            // Run once at startup, then on the interval.
            await RunOnce(stoppingToken);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnce(stoppingToken);
            }
        }

        public async Task RunOnce(CancellationToken cancellationToken)
        {
            try
            {
                var retentionDays = configuration.GetValue<int>("TrashRetention:Days", 30);
                var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);

                using var scope = scopeFactory.CreateScope();
                var repoFactory = scope.ServiceProvider.GetRequiredService<IRepositoryFactory>();

                // Purge tasks first to avoid FK interaction with cascaded parent purge.
                var taskRepo = repoFactory.GetRepository<TrackedTask>();
                var projectRepo = repoFactory.GetRepository<Project>();

                var taskCount = await taskRepo.PurgeExpired(cutoff);
                var projectCount = await projectRepo.PurgeExpired(cutoff);

                logger.LogInformation("TrashPurge: purged {taskCount} tasks and {projectCount} projects older than {cutoff}",
                    taskCount, projectCount, cutoff);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "TrashPurge tick failed");
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~TrashPurgeServiceTest"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Timinute/Server/Services/TrashPurgeService.cs \
        Timinute/Server.Tests/Services/TrashPurgeServiceTest.cs
git commit -m "feat: add TrashPurgeService background service"
```

---

### Task 17: Register `TrashPurgeService` in `Program.cs`

**Files:**
- Modify: `Timinute/Server/Program.cs`

- [ ] **Step 1: Register the hosted service**

In `Program.cs`, find the `DependecyInjection()` function (around line 238) and append to it:

```csharp
void DependecyInjection()
{
    // DI
    builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
    builder.Services.AddTransient<IRepositoryFactory, RepositoryFactory>();
    builder.Services.AddSingleton<IExportService, ExportService>();
    builder.Services.AddHostedService<TrashPurgeService>();
}
```

The `using Timinute.Server.Services;` already exists at the top.

- [ ] **Step 2: Verify build + run**

Run: `dotnet build Timinute.sln`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Timinute/Server/Program.cs
git commit -m "feat: register TrashPurgeService as hosted background service"
```

---

## Phase 7: Client UX

### Task 18: Undo-toast helper

**Files:**
- Create: `Timinute/Client/Services/UndoNotificationService.cs`

**Implementation note:** Radzen's `NotificationMessage` supports a `Click` handler which fires when the toast is clicked. Use this to trigger the restore action; no custom action-button component needed.

- [ ] **Step 1: Create the helper**

```csharp
using Radzen;

namespace Timinute.Client.Services
{
    public class UndoNotificationService
    {
        private readonly NotificationService notificationService;

        public UndoNotificationService(NotificationService notificationService)
        {
            this.notificationService = notificationService;
        }

        public void ShowUndo(string entityLabel, string entityName, Func<Task> onUndo)
        {
            var message = new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = $"{entityLabel} '{entityName}' deleted",
                Detail = "Click to undo.",
                Duration = 8000,
                Click = async _ =>
                {
                    try
                    {
                        await onUndo();
                        notificationService.Notify(new NotificationMessage
                        {
                            Severity = NotificationSeverity.Success,
                            Summary = $"{entityLabel} restored",
                            Duration = 4000
                        });
                    }
                    catch (Exception ex)
                    {
                        notificationService.Notify(new NotificationMessage
                        {
                            Severity = NotificationSeverity.Error,
                            Summary = "Restore failed",
                            Detail = ex.Message,
                            Duration = 5000
                        });
                    }
                }
            };

            notificationService.Notify(message);
        }
    }
}
```

- [ ] **Step 2: Register in `Timinute/Client/Program.cs`**

Open `Timinute/Client/Program.cs`. Find the line that registers `NotificationService` (if absent, add both). Add:

```csharp
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<Timinute.Client.Services.UndoNotificationService>();
```

If `NotificationService` is already registered by Radzen elsewhere (check `Radzen.Blazor` wiring in the existing `Program.cs`), keep only the `UndoNotificationService` line.

- [ ] **Step 3: Verify build**

Run: `dotnet build Timinute.sln`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Timinute/Client/Services/UndoNotificationService.cs \
        Timinute/Client/Program.cs
git commit -m "feat: add UndoNotificationService helper for delete toasts"
```

---

### Task 19: Wire undo-toast into existing delete flows

**Files:**
- Modify: the Blazor components that currently call `DELETE /api/TrackedTask/{id}` and `DELETE /api/Project/{id}`.

**Discovery note:** the exact component names depend on the current client structure. Search:

- `rg -n "DELETE|DeleteAsync" Timinute/Client/` to find delete call sites.
- Common candidates: `Components/TrackedTasks/TrackedTaskTable.razor`, the Projects list component, `Pages/TimeTracker.razor`.

For each delete call site:

- [ ] **Step 1: Inject `UndoNotificationService` at the top of the component**

```razor
@inject Timinute.Client.Services.UndoNotificationService UndoNotifier
@inject HttpClient Http
```

- [ ] **Step 2: After the successful delete, call `ShowUndo`**

Wrap the existing delete + refresh logic:

```csharp
private async Task DeleteTask(TrackedTaskDto task)
{
    var response = await Http.DeleteAsync($"/api/TrackedTask/{task.TaskId}");
    if (response.IsSuccessStatusCode)
    {
        await RefreshList(); // existing refresh

        UndoNotifier.ShowUndo("Task", task.Name, async () =>
        {
            await Http.PostAsync($"/api/TrackedTask/{task.TaskId}/restore", null);
            await RefreshList();
        });
    }
}
```

For Projects, substitute `"Project"`, `project.Name`, and the Project URLs.

- [ ] **Step 3: Manual verification**

Run: `dotnet run --project Timinute/Server/Timinute.Server.csproj`

In a browser: delete a task → toast appears → click toast → task reappears. Repeat for a project.

- [ ] **Step 4: Commit**

```bash
git add Timinute/Client/
git commit -m "feat: show undo toast after project and task delete"
```

---

### Task 20: Trash page

**Files:**
- Create: `Timinute/Client/Pages/Trash.razor`
- Modify: `Timinute/Client/Shared/NavMenu.razor`

- [ ] **Step 1: Create `Trash.razor`**

```razor
@page "/trash"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@inject HttpClient Http
@inject Radzen.NotificationService Notifier
@inject Radzen.DialogService DialogService
@using Timinute.Shared.Dtos.Trash

<h1>Trash</h1>

<p>Items here are permanently deleted after 30 days.</p>

<h3>Deleted Projects</h3>
<RadzenDataGrid Data="@deletedProjects" TItem="TrashItemDto" EmptyText="No deleted projects.">
    <Columns>
        <RadzenDataGridColumn TItem="TrashItemDto" Property="Name" Title="Name" />
        <RadzenDataGridColumn TItem="TrashItemDto" Property="DeletedAt" Title="Deleted on" FormatString="{0:g}" />
        <RadzenDataGridColumn TItem="TrashItemDto" Property="DaysRemaining" Title="Days remaining" />
        <RadzenDataGridColumn TItem="TrashItemDto" Title="Actions" Sortable="false" Filterable="false">
            <Template Context="item">
                <RadzenButton Text="Restore" Size="ButtonSize.Small" Click="@(async () => await RestoreProject(item.Id))" />
                <RadzenButton Text="Delete Permanently" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Danger"
                              Click="@(async () => await PurgeProject(item))" />
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>

<h3>Deleted Tasks</h3>
<RadzenDataGrid Data="@deletedTasks" TItem="TrashItemDto" EmptyText="No deleted tasks.">
    <Columns>
        <RadzenDataGridColumn TItem="TrashItemDto" Property="Name" Title="Name" />
        <RadzenDataGridColumn TItem="TrashItemDto" Property="DeletedAt" Title="Deleted on" FormatString="{0:g}" />
        <RadzenDataGridColumn TItem="TrashItemDto" Property="DaysRemaining" Title="Days remaining" />
        <RadzenDataGridColumn TItem="TrashItemDto" Title="Actions" Sortable="false" Filterable="false">
            <Template Context="item">
                <RadzenButton Text="Restore" Size="ButtonSize.Small" Click="@(async () => await RestoreTask(item.Id))" />
                <RadzenButton Text="Delete Permanently" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Danger"
                              Click="@(async () => await PurgeTask(item))" />
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>

@code {
    private List<TrashItemDto> deletedProjects = new();
    private List<TrashItemDto> deletedTasks = new();

    protected override async Task OnInitializedAsync() => await Refresh();

    private async Task Refresh()
    {
        deletedProjects = await Http.GetFromJsonAsync<List<TrashItemDto>>("/api/Project/trash") ?? new();
        deletedTasks = await Http.GetFromJsonAsync<List<TrashItemDto>>("/api/TrackedTask/trash") ?? new();
    }

    private async Task RestoreProject(string id)
    {
        await Http.PostAsync($"/api/Project/{id}/restore", null);
        Notifier.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Project restored", Duration = 3000 });
        await Refresh();
    }

    private async Task RestoreTask(string id)
    {
        await Http.PostAsync($"/api/TrackedTask/{id}/restore", null);
        Notifier.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Task restored", Duration = 3000 });
        await Refresh();
    }

    private async Task PurgeProject(TrashItemDto item)
    {
        var confirm = await DialogService.Confirm($"Permanently delete project '{item.Name}'? This cannot be undone.", "Confirm", new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });
        if (confirm == true)
        {
            await Http.DeleteAsync($"/api/Project/{item.Id}/purge");
            await Refresh();
        }
    }

    private async Task PurgeTask(TrashItemDto item)
    {
        var confirm = await DialogService.Confirm($"Permanently delete task '{item.Name}'? This cannot be undone.", "Confirm", new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });
        if (confirm == true)
        {
            await Http.DeleteAsync($"/api/TrackedTask/{item.Id}/purge");
            await Refresh();
        }
    }
}
```

- [ ] **Step 2: Add nav link to `NavMenu.razor`**

Open `Timinute/Client/Shared/NavMenu.razor`. Find the existing nav list (look for `<NavLink>` entries) and add:

```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="trash">
        <span class="oi oi-trash" aria-hidden="true"></span> Trash
    </NavLink>
</div>
```

Place it near the bottom of the same nav section used by other authenticated links. If the nav has category headers, add under an existing "Recovery" category or append after the last link.

- [ ] **Step 3: Manual verification**

Run: `dotnet run --project Timinute/Server/Timinute.Server.csproj`

Navigate to `/trash`. Delete a task and a project elsewhere, then reload `/trash`. Verify both lists populate, both actions (Restore / Delete Permanently) work, and day counts render sensibly.

- [ ] **Step 4: Commit**

```bash
git add Timinute/Client/Pages/Trash.razor \
        Timinute/Client/Shared/NavMenu.razor
git commit -m "feat: add client Trash page and nav link"
```

---

## Phase 8: Final Verification

### Task 21: Full build + test + runtime check

- [ ] **Step 1: Clean build**

Run: `dotnet clean Timinute.sln && dotnet build Timinute.sln`
Expected: 0 errors, 0 warnings beyond pre-existing ones.

- [ ] **Step 2: Run full test suite**

Run: `dotnet test --verbosity normal`
Expected: all tests pass.

- [ ] **Step 3: Apply migration to dev DB**

Run: `dotnet ef database update --project Timinute/Server/Timinute.Server.csproj`
Expected: `AddSoftDelete` migration applies cleanly.

- [ ] **Step 4: Run app and smoke-test E2E flows**

Run: `dotnet run --project Timinute/Server/Timinute.Server.csproj`

Manually verify in browser:
1. Log in as a seeded test user.
2. Delete a task → toast appears → click toast → task reappears in list.
3. Delete a task → navigate to `/trash` → task listed → click Restore → task back in main list.
4. Delete a task → navigate to `/trash` → click Delete Permanently → confirm → task disappears from trash.
5. Delete a project that has child tasks → navigate to `/trash` → project listed → click Restore → project AND its tasks reappear in their lists.
6. Swagger `/swagger` shows the four new endpoints on each controller (restore, trash, purge).

- [ ] **Step 5: Verify background service logged on startup**

Check server console for `TrashPurge: purged 0 tasks and 0 projects older than ...` within the first few seconds of app start.

- [ ] **Step 6: Clean working tree check**

Run: `git status`
Expected: no untracked files, no unstaged changes.

---

## Done

All P0 soft delete work is implemented. The next P0 roadmap items (analytics optimization, timer edge cases) remain open and get their own plans.
