# Soft Delete Design

## Goal

Make `DELETE` on Project and TrackedTask recoverable for 30 days, then hard-purge. Protects against accidental deletion without changing the conceptual "delete" action for users.

## Scope

- Soft delete applies to `Project` and `TrackedTask` only.
- Primary use case: undo accidental deletion. Not an "archive for history" feature.
- Soft-deleting a Project cascades to its TrackedTasks; restoring a Project restores only the tasks that were cascaded with it.
- Retention window: 30 days from deletion, then automatic hard-purge.
- Client surfaces: undo toast after every delete + dedicated `/trash` page.

**Out of scope:** bulk operations ("Empty Trash"), email notifications before purge, user-configurable retention, undo-after-purge, soft delete on User or any other entity.

## Data Model

Add one nullable column to each entity:

```csharp
// Project.cs, TrackedTask.cs
public DateTimeOffset? DeletedAt { get; set; }
```

`null` = active. Non-null timestamp = soft-deleted and purge-eligible after 30 days. No separate `IsDeleted` bool — the timestamp itself is the flag.

### Marker interface

```csharp
// Timinute.Server.Models (or Repository)
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }
}
```

Applied to `Project` and `TrackedTask`. Used by the repository layer to constrain the generic soft-delete methods (compile-time safety against using them on non-soft-deletable types).

### EF configuration (`ApplicationDbContext.OnModelCreating`)

- Global query filter per entity: `HasQueryFilter(e => e.DeletedAt == null)`. Every existing LINQ query automatically excludes deleted rows. Trash queries opt out via `IgnoreQueryFilters()`.
- Index on `DeletedAt` for both tables (speeds purge scans + trash view).
- Existing `Project→TrackedTask OnDelete(SetNull)` FK stays — governs *hard* deletes only. Soft-delete cascade is handled in application code, not the DB.

## API Surface

Four endpoints per soft-deletable entity. Existing `DELETE` semantics change from hard-delete to soft-delete.

| Method | Route | Behavior |
|---|---|---|
| `DELETE` | `/api/Project/{id}` | Soft-deletes. Cascades to tasks (same timestamp). |
| `POST` | `/api/Project/{id}/restore` | Clears `DeletedAt`. For projects, restores sibling tasks matching `task.ProjectId == id && task.DeletedAt == project.DeletedAt`. |
| `GET` | `/api/Project/trash` | Lists user's soft-deleted items with `DeletedAt` + computed `DaysRemaining`. Uses `IgnoreQueryFilters()`. |
| `DELETE` | `/api/Project/{id}/purge` | Immediate hard-delete. Owner only. |

Same four for `TrackedTask`.

All endpoints enforce existing ownership check (`userId == entity.UserId`). `restore` and `purge` return 404 if entity not found, not owned, or not in soft-deleted state (as appropriate).

### Cascade semantics

- **Soft-delete a Project:** controller sets `project.DeletedAt = UtcNow`, then sets the *same* timestamp on every task where `ProjectId == id && DeletedAt == null`. Single `SaveChanges`.
- **Restore a Project:** controller sets `project.DeletedAt = null`, then clears `DeletedAt` on tasks matching `ProjectId == id && DeletedAt == (original project timestamp)`. This ensures tasks the user deleted individually *before* the project was deleted stay in trash.
- **Soft-delete a TrackedTask individually:** no cascade; just sets its own `DeletedAt`.

## Repository Layer

### `IRepository<T>` additions

```csharp
Task SoftDelete(object id);
Task Restore(object id);
Task<IEnumerable<T>> GetDeleted(Expression<Func<T, bool>>? filter = null);
Task<int> PurgeExpired(DateTimeOffset olderThan);
```

### `BaseRepository<T>` implementation

- Generic constraint: `where T : class, ISoftDeletable` on the soft-delete methods (or move to a derived `SoftDeleteRepository<T>` if the existing generic constraint can't be tightened — decide at implementation time).
- `SoftDelete(id)` — load entity, set `DeletedAt = UtcNow`, `SaveChanges`.
- `Restore(id)` — load entity with `IgnoreQueryFilters()`, set `DeletedAt = null`, `SaveChanges`.
- `GetDeleted(filter)` — `DbSet.IgnoreQueryFilters().Where(e => e.DeletedAt != null).Where(filter ?? _ => true)`.
- `PurgeExpired(olderThan)` — `DbSet.IgnoreQueryFilters().Where(e => e.DeletedAt != null && e.DeletedAt < olderThan).ExecuteDeleteAsync()`. Returns rows affected.
- Existing `Delete(id)` / `Delete(entity)` stays as-is (hard-delete) — used by the purge endpoint and as the fallback path in the background service.

## Background Purge

`TrashPurgeService : BackgroundService` in `Timinute/Server/Services/`.

- Registered via `builder.Services.AddHostedService<TrashPurgeService>()` in `Program.cs`.
- `PeriodicTimer` with interval from `appsettings.json` key `TrashRetention:PurgeInterval` (default `TimeSpan.FromHours(24)`).
- Retention window from `TrashRetention:Days` (default `30`).
- Each tick:
  1. Create scoped service provider → resolve `IRepositoryFactory`.
  2. Call `PurgeExpired(UtcNow - retentionDays)` on `TrackedTask` repo first, then `Project` repo. Purging tasks first avoids any chance of orphan weirdness if a project ages past retention while cascaded tasks are still present.
  3. Log `Information`: `"Purged {taskCount} tasks and {projectCount} projects older than {cutoff}"`.
- Wrap tick in `try/catch` — log exceptions but never rethrow. A bad tick must not crash the host.
- Extract the purge body into a `public async Task RunOnce(CancellationToken)` method so tests can invoke a single pass without waiting on the timer.

## Client UX

### Undo toast

After a successful `DELETE` request from any page, raise a Radzen `NotificationService` toast:

- Summary: `"Project 'Foo' deleted"` (or `"Task 'Bar' deleted"`).
- Detail: `"Undo"` — styled as an action link that calls the `/restore` endpoint and refreshes the current view.
- Severity: `Info`. Duration: `8000ms`.
- On restore success: second toast `"Project restored"`.

Implementation sketch: wrap Radzen's `NotificationMessage` with a custom `Click` handler passed via a small `UndoableNotificationService` helper.

### Trash page

New route `Client/Pages/Trash.razor` at path `/trash`. Linked from `NavMenu` under a "Recovery" category (new) or appended to existing menu — pick at implementation time to match redesign direction.

- Two Radzen `DataGrid` sections, stacked: **Deleted Projects** and **Deleted Tasks**.
- Columns: `Name`, `Deleted on` (localized), `Days remaining` (`30 - (now - DeletedAt).TotalDays`, rounded up), Actions (`Restore`, `Delete Permanently`).
- Empty state per section: `"No deleted items."`.
- `Delete Permanently` prompts a Radzen `DialogService` confirm before calling `/purge`.
- Both actions refresh both grids on success.
- No pagination — auto-purge keeps the list small. Revisit if needed.

### Unaffected surfaces

- Analytics endpoints and Dashboard charts: no change. Global query filter hides deleted rows.
- Calendar/scheduler: no change for same reason.
- Existing list pages (Projects, TrackedTasks): no change beyond the post-delete toast.

## Database Migration

Single EF migration: `AddSoftDelete`.

- Adds `DeletedAt DATETIMEOFFSET NULL` to `Projects` and `TrackedTasks`.
- Adds index on `DeletedAt` for both tables.
- No data backfill — existing rows default to `NULL` (active).
- Seed data unchanged.

## Testing

Test-driven. Each step below gets a failing test first.

### Repository tests

- `SoftDelete` sets `DeletedAt` and row disappears from default `Get` / `GetPaged`.
- `GetDeleted` returns only rows with `DeletedAt != null` and respects the optional filter.
- `Restore` clears `DeletedAt` and the row reappears in default queries.
- `PurgeExpired(cutoff)` removes rows where `DeletedAt < cutoff` and leaves fresher deletions + active rows untouched.
- Existing hard `Delete` still removes the row entirely.

### Controller tests

- Soft-delete + restore roundtrip for Project and TrackedTask.
- Soft-deleting a Project cascades to its tasks with matching `DeletedAt`.
- Restoring a Project restores only tasks whose `DeletedAt == project.DeletedAt`; individually-deleted tasks stay in trash.
- `trash` endpoint returns only deleted items owned by caller.
- `purge` returns 404 for non-owner; 200 + removes row for owner.
- `DELETE` on already-soft-deleted item returns 404 (not idempotent — treat as "not found" after first soft-delete).

### Background service test

- `TrashPurgeService.RunOnce` against InMemory DB with seeded rows at varying ages purges only expired rows and logs the count.

### Regression

- Full existing test suite (50+) stays green — seed data has no deleted rows, so the query filter is a no-op for all current queries.

## Open Decisions (resolve at implementation time)

- Generic constraint vs derived repository for soft-delete methods (depends on whether `BaseRepository<T>` can tighten its `where T : class` constraint without breaking other callers).
- NavMenu category for `/trash` — fits naturally in a future "Recovery" or under user profile dropdown; coordinate with the in-flight theming work.
