# P1 Follow-ups Bundle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship four review-raised P1 follow-ups as one bundled PR: (1) make `StartDate` on the TrackedTask DTOs non-nullable, (2) add server-side aggregation methods to the repository and use them in `GetMe`, (3) notify Blazor when the OS color scheme changes for `System`-theme users, (4) introduce `UserProfileService` so `/User/me` is fetched once per session.

**Architecture:** Item 1 is a 2-line DTO edit plus a test sweep. Item 2 adds two methods to `IRepository<T>` / `BaseRepository<T>` and rewrites `UserController.GetMe` to use them. Item 3 extends the existing `window.__theme` JS bootstrap with a `register`/`unregister` callback and adds a `[JSInvokable]` to `ThemeService`. Item 4 creates a new `UserProfileService` that owns a `Task<UserProfileDto?>` cache; `ThemeService` is refactored to consume it; `Profile.razor` and `Dashboard.razor` swap their direct HTTP calls for it.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10, Blazor WebAssembly, xUnit + Moq + EF InMemory, vanilla JS (theme bootstrap).

**Source spec:** `docs/superpowers/specs/2026-05-16-p1-followups-design.md` (commit `b25b48e`).

**Working branch:** All tasks run on a new feature branch `feature/p1-followups`, PR'd into `develop`.

---

## Task 0: Create the feature branch

**Files:** none

- [ ] **Step 1: Confirm clean tree on `develop`**

Run: `git -C "D:\Projects\WebApps\Timinute" status && git -C "D:\Projects\WebApps\Timinute" rev-parse --abbrev-ref HEAD`
Expected: `nothing to commit, working tree clean` and current branch `develop`.

- [ ] **Step 2: Create the feature branch**

Run: `git -C "D:\Projects\WebApps\Timinute" checkout -b feature/p1-followups`
Expected: `Switched to a new branch 'feature/p1-followups'`

---

## Task 1: `StartDate` DTO type fix

**Files:**
- Modify: `Timinute/Shared/Dtos/TrackedTask/CreateTrackedTaskDto.cs` (1 line)
- Modify: `Timinute/Shared/Dtos/TrackedTask/UpdateTrackedTaskDto.cs` (1 line)
- Modify: any test file that constructs the DTOs with `StartDate = null` (sweep)

- [ ] **Step 1: Edit `CreateTrackedTaskDto.cs`**

Replace the `StartDate` line:

```diff
     [Required]
-    public DateTimeOffset? StartDate { get; set; }
+    public DateTimeOffset StartDate { get; set; }
```

Use `Edit` with the literal `public DateTimeOffset? StartDate { get; set; }` as the anchor (it appears only once in this file).

- [ ] **Step 2: Edit `UpdateTrackedTaskDto.cs`**

Same change, same diff. Same single-occurrence anchor.

- [ ] **Step 3: Find any test code that assigns `null` to `StartDate`**

Run the Grep tool with:
- pattern: `StartDate = null`
- path: `D:\Projects\WebApps\Timinute\Timinute\Server.Tests`
- output_mode: `content`
- `-n`: true

Note every match. Most likely zero — the existing tests construct DTOs with real seed timestamps. If matches are found, fix each by replacing `null` with a sensible `DateTimeOffset` literal (e.g. `new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero)` to match the existing seed convention).

Also run with:
- pattern: `StartDate\s*=\s*default`
- path: same

Fix the same way if found.

- [ ] **Step 4: Build the solution**

Run from `D:\Projects\WebApps\Timinute`: `dotnet build Timinute.sln --configuration Release`
Expected: `Build succeeded`. Zero errors. Any new compile errors at this point indicate Step 3 missed a `null` assignment — go back, fix, rebuild.

- [ ] **Step 5: Run the test suite**

Run: `dotnet test Timinute.sln --configuration Release --no-build`
Expected: all 101 existing tests pass. The two `Get_Me_Returns_Profile_*` tests pass without modification (they don't construct CreateTrackedTaskDto with null StartDate).

- [ ] **Step 6: Commit**

```bash
git -C "D:\Projects\WebApps\Timinute" add Timinute/Shared/Dtos/TrackedTask/
git -C "D:\Projects\WebApps\Timinute" commit -m "feat(dto): make TrackedTask StartDate non-nullable

BREAKING-CHANGE: wire format for CreateTrackedTaskDto/UpdateTrackedTaskDto
StartDate changes from 'string|null' to 'string' (DateTimeOffset, [Required]).
No external API consumers today. The existing EndDate > StartDate and
MinDuration validators catch any missing/zero StartDate input."
```

If Step 3 found and fixed test files, they go in the same `git add` (re-stage them: `git add Timinute/Server.Tests/`).

---

## Task 2: Add `CountAsync` + `SumAsync` to `IRepository<T>`

**Files:**
- Modify: `Timinute/Server/Repository/IRepository.cs`
- Modify: `Timinute/Server/Repository/BaseRepository.cs`
- Create: `Timinute/Server.Tests/Repositories/RepositoryAggregationTest.cs`

- [ ] **Step 1: Write the failing tests (TDD)**

Create `Timinute/Server.Tests/Repositories/RepositoryAggregationTest.cs` with this exact content:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Server.Tests.Helpers;
using Xunit;

namespace Timinute.Server.Tests.Repositories
{
    public class RepositoryAggregationTest
    {
        private const string _databaseName = "RepositoryAggregation_Test_DB";

        [Fact]
        public async Task CountAsync_WithFilter_ReturnsMatchingCount()
        {
            var db = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CountFilter");
            var repo = new BaseRepository<TrackedTask>(db);

            // Seed has 4 tasks for ApplicationUser1 (TrackedTaskId1..4)
            var count = await repo.CountAsync(t => t.UserId == "ApplicationUser1");

            Assert.Equal(4, count);
        }

        [Fact]
        public async Task CountAsync_NoFilter_ReturnsTotal()
        {
            var db = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CountAll");
            var repo = new BaseRepository<TrackedTask>(db);

            // Seed total tracked tasks (all users combined) — see TestHelper.FillInitData
            var count = await repo.CountAsync();

            Assert.True(count > 0, "expected seed data to provide at least one tracked task");
        }

        [Fact]
        public async Task CountAsync_RespectsGlobalQueryFilter_ExcludesSoftDeleted()
        {
            var db = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CountSoftDeleted");
            var repo = new BaseRepository<TrackedTask>(db);

            // Soft-delete one of ApplicationUser1's tasks
            await repo.SoftDelete("TrackedTaskId1");

            var count = await repo.CountAsync(t => t.UserId == "ApplicationUser1");

            // 4 seeded - 1 soft-deleted = 3 visible through the global filter
            Assert.Equal(3, count);
        }

        [Fact]
        public async Task SumAsync_WithFilter_ReturnsFilteredSum()
        {
            var db = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "SumFilter");
            var repo = new BaseRepository<TrackedTask>(db);

            // ApplicationUser1 tasks: 2h + 3h + 4h + 4h + 1h = ... use seed durations.
            // Compute expected by summing the same set client-side from the same context.
            var expectedTicks = await db.TrackedTasks
                .Where(t => t.UserId == "ApplicationUser1")
                .SumAsync(t => t.Duration.Ticks);

            var actualTicks = await repo.SumAsync(t => t.Duration.Ticks, t => t.UserId == "ApplicationUser1");

            Assert.Equal(expectedTicks, actualTicks);
            Assert.True(actualTicks > 0, "expected non-zero seed durations");
        }

        [Fact]
        public async Task SumAsync_EmptySet_ReturnsZero()
        {
            var db = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "SumEmpty");
            var repo = new BaseRepository<TrackedTask>(db);

            var totalTicks = await repo.SumAsync(t => t.Duration.Ticks, t => t.UserId == "no-such-user");

            Assert.Equal(0L, totalTicks);
        }
    }
}
```

- [ ] **Step 2: Run the tests — confirm they fail to compile**

Run from `D:\Projects\WebApps\Timinute`: `dotnet build Timinute.sln --configuration Release 2>&1 | head -40`
Expected: build fails. Error messages mention `'IRepository<TrackedTask>' does not contain a definition for 'CountAsync'` and `'SumAsync'`. This is the failing-test stage.

- [ ] **Step 3: Add the method signatures to `IRepository<T>`**

In `Timinute/Server/Repository/IRepository.cs`, find the line:

```csharp
        Task<int> CountAll(Expression<Func<TEntity, bool>>? filter = null);
```

Insert immediately AFTER it (before the closing `}` of the interface):

```csharp

        /// <summary>
        /// Asynchronously counts entities matching the optional filter.
        /// Honors EF global query filters (e.g. soft delete) — use <see cref="CountAll"/>
        /// when you need the unfiltered (including soft-deleted) count.
        /// </summary>
        Task<int> CountAsync(Expression<Func<TEntity, bool>>? filter = null);

        /// <summary>
        /// Asynchronously sums the projected long values for entities matching
        /// the optional filter. Translated server-side by EF Core when the
        /// selector hits column-mapped properties.
        /// </summary>
        Task<long> SumAsync(
            Expression<Func<TEntity, long>> selector,
            Expression<Func<TEntity, bool>>? filter = null);
```

- [ ] **Step 4: Implement in `BaseRepository<T>`**

In `Timinute/Server/Repository/BaseRepository.cs`, find the existing method:

```csharp
        public async Task<int> CountAll(Expression<Func<TEntity, bool>>? filter = null)
        {
            IQueryable<TEntity> query = dbSet.IgnoreQueryFilters();
            if (filter != null)
                query = query.Where(filter);
            return await query.CountAsync();
        }
```

Insert immediately AFTER it (before the next method):

```csharp

        public async Task<int> CountAsync(Expression<Func<TEntity, bool>>? filter = null)
        {
            IQueryable<TEntity> query = dbSet;
            if (filter != null)
                query = query.Where(filter);
            return await query.CountAsync();
        }

        public async Task<long> SumAsync(
            Expression<Func<TEntity, long>> selector,
            Expression<Func<TEntity, bool>>? filter = null)
        {
            IQueryable<TEntity> query = dbSet;
            if (filter != null)
                query = query.Where(filter);
            return await query.Select(selector).SumAsync();
        }
```

Note: `CountAll` uses `IgnoreQueryFilters()` (intentional — see its existing doc-comment about monotonic counters). The new `CountAsync` does NOT call `IgnoreQueryFilters()`, so soft-deleted rows are excluded as expected by `GetMe`.

- [ ] **Step 5: Build**

Run from `D:\Projects\WebApps\Timinute`: `dotnet build Timinute.sln --configuration Release`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 6: Run the new tests**

Run: `dotnet test Timinute.sln --configuration Release --no-build --filter "FullyQualifiedName~RepositoryAggregationTest"`
Expected: `Passed: 5, Failed: 0`.

If `SumAsync_WithFilter_ReturnsFilteredSum` fails with a `System.InvalidOperationException` mentioning "could not be translated" (EF Core couldn't convert `t.Duration.Ticks` to SQL), apply the fallback in Step 7 below. Otherwise, skip to Step 8.

- [ ] **Step 7 (CONDITIONAL — only if EF can't translate `Duration.Ticks`):** Adjust `SumAsync` to client-side fallback

Replace the body of `SumAsync` in `BaseRepository.cs` with:

```csharp
        public async Task<long> SumAsync(
            Expression<Func<TEntity, long>> selector,
            Expression<Func<TEntity, bool>>? filter = null)
        {
            IQueryable<TEntity> query = dbSet;
            if (filter != null)
                query = query.Where(filter);

            // Server-side SUM where supported; falls back to client-side
            // for selectors EF Core 10 can't translate (e.g. TimeSpan.Ticks
            // on SQL Server time(7) columns).
            try
            {
                return await query.Select(selector).SumAsync();
            }
            catch (InvalidOperationException)
            {
                var projected = await query.Select(selector).ToListAsync();
                return projected.Sum();
            }
        }
```

Rerun Step 6. Expected: green.

- [ ] **Step 8: Run the full test suite — confirm no regressions**

Run: `dotnet test Timinute.sln --configuration Release --no-build`
Expected: 106 total tests, 0 failed (101 existing + 5 new).

- [ ] **Step 9: Commit**

```bash
git -C "D:\Projects\WebApps\Timinute" add Timinute/Server/Repository/IRepository.cs Timinute/Server/Repository/BaseRepository.cs Timinute/Server.Tests/Repositories/RepositoryAggregationTest.cs
git -C "D:\Projects\WebApps\Timinute" commit -m "feat(repo): add CountAsync + SumAsync with filter expressions

Both translate to SQL aggregates server-side. CountAsync respects EF
global query filters (soft delete); use CountAll for the unfiltered form.
SumAsync<long> covers the immediate need (Duration.Ticks aggregation);
overloads for int/decimal can be added later if needed.

5 new unit tests against EF InMemory cover happy path, filter, soft-delete
exclusion, and empty-set return-zero."
```

---

## Task 3: Refactor `UserController.GetMe` to use server-side aggregation

**Files:**
- Modify: `Timinute/Server/Controllers/UserController.cs`

- [ ] **Step 1: Replace the in-memory aggregation block**

In `Timinute/Server/Controllers/UserController.cs`, find this block:

```csharp
            var tasks = (await taskRepository.Get(t => t.UserId == userId)).ToList();
            var projectCount = (await projectRepository.Get(p => p.UserId == userId)).Count();

            var totalTicks = tasks.Aggregate(0L, (acc, t) => acc + t.Duration.Ticks);

            return Ok(new UserProfileDto
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                CreatedAt = user.CreatedAt,
                TotalTrackedTime = TimeSpan.FromTicks(totalTicks),
                ProjectCount = projectCount,
                TaskCount = tasks.Count,
                Preferences = mapper.Map<UserPreferencesDto>(user.Preferences ?? new UserPreferences())
            });
```

Replace with:

```csharp
            var taskCount    = await taskRepository.CountAsync(t => t.UserId == userId);
            var projectCount = await projectRepository.CountAsync(p => p.UserId == userId);
            var totalTicks   = await taskRepository.SumAsync(t => t.Duration.Ticks, t => t.UserId == userId);

            return Ok(new UserProfileDto
            {
                FirstName        = user.FirstName,
                LastName         = user.LastName,
                Email            = user.Email ?? string.Empty,
                CreatedAt        = user.CreatedAt,
                TotalTrackedTime = TimeSpan.FromTicks(totalTicks),
                ProjectCount     = projectCount,
                TaskCount        = taskCount,
                Preferences      = mapper.Map<UserPreferencesDto>(user.Preferences ?? new UserPreferences())
            });
```

Use Edit with the literal `var tasks = (await taskRepository.Get(t => t.UserId == userId)).ToList();` as a unique anchor.

- [ ] **Step 2: Build**

Run: `dotnet build Timinute.sln --configuration Release`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test Timinute.sln --configuration Release --no-build`
Expected: 106 / 0 failed. The two `Get_Me_*` tests pass without modification — they verify the response shape, which is unchanged.

- [ ] **Step 4: Commit**

```bash
git -C "D:\Projects\WebApps\Timinute" add Timinute/Server/Controllers/UserController.cs
git -C "D:\Projects\WebApps\Timinute" commit -m "perf(user): aggregate GetMe counts/totals server-side

Replace the load-everything-and-aggregate loop with three SQL aggregates
(COUNT, COUNT, SUM). For users with thousands of tasks this drops from
'load N rows into memory' to three small indexed queries. Response shape
unchanged; existing GetMe tests cover the equivalence assertion."
```

---

## Task 4: OS-theme-change JS bootstrap extension

**Files:**
- Modify: `Timinute/Client/wwwroot/js/theme-bootstrap.js`

- [ ] **Step 1: Add `dotnetRef` state + extend the listener + add `register`/`unregister`**

Open `Timinute/Client/wwwroot/js/theme-bootstrap.js`. Apply these two changes:

**1a)** Find the IIFE opening line `(function () {` and the constants right below it. Insert a new `dotnetRef` declaration AFTER `const root = document.documentElement;` (around line 11):

Use Edit with this old_string:

```javascript
    const KEY = 'timinute:theme';
    const root = document.documentElement;

    function resolve(stored) {
```

And this new_string:

```javascript
    const KEY = 'timinute:theme';
    const root = document.documentElement;
    let dotnetRef = null;

    function resolve(stored) {
```

**1b)** Find the `matchMedia` listener block (around lines 33-42 in the original):

Use Edit with this old_string:

```javascript
        if (mql.addEventListener) {
            mql.addEventListener('change', function () {
                let cur;
                try { cur = localStorage.getItem(KEY) || 'System'; } catch { cur = 'System'; }
                if (cur === 'System') apply('System');
            });
        }
```

And this new_string:

```javascript
        if (mql.addEventListener) {
            mql.addEventListener('change', function () {
                let cur;
                try { cur = localStorage.getItem(KEY) || 'System'; } catch { cur = 'System'; }
                if (cur === 'System') {
                    apply('System');
                    if (dotnetRef) {
                        try { dotnetRef.invokeMethodAsync('NotifyResolvedThemeChangedAsync'); }
                        catch { /* circuit gone */ }
                    }
                }
            });
        }
```

**1c)** Find the `window.__theme` object literal (last block in the IIFE):

Use Edit with this old_string:

```javascript
    window.__theme = {
        set: function (stored) {
            try { localStorage.setItem(KEY, stored); } catch { /* private mode / quota — non-fatal */ }
            apply(stored);
        },
        get: function () {
            try { return localStorage.getItem(KEY) || 'System'; } catch { return 'System'; }
        },
        // Returns "dark" or "light" — the resolved value currently on
        // <html data-theme>. Used by the topbar toggle to render the
        // correct icon when the stored value is "System".
        getResolved: function () {
            return root.getAttribute('data-theme') || 'light';
        },
    };
```

And this new_string:

```javascript
    window.__theme = {
        set: function (stored) {
            try { localStorage.setItem(KEY, stored); } catch { /* private mode / quota — non-fatal */ }
            apply(stored);
        },
        get: function () {
            try { return localStorage.getItem(KEY) || 'System'; } catch { return 'System'; }
        },
        // Returns "dark" or "light" — the resolved value currently on
        // <html data-theme>. Used by the topbar toggle to render the
        // correct icon when the stored value is "System".
        getResolved: function () {
            return root.getAttribute('data-theme') || 'light';
        },
        // Blazor registers a DotNetObjectReference here on first render
        // (via ThemeService.RegisterOsChangeListenerAsync). When the OS
        // color scheme changes AND the user is on 'System', the listener
        // above invokes ThemeService.NotifyResolvedThemeChangedAsync,
        // which fires the Changed event so the Topbar icon re-renders.
        register: function (ref) { dotnetRef = ref; },
        unregister: function () { dotnetRef = null; },
    };
```

- [ ] **Step 2: Manual sanity check the file**

Run: open the file with Read and visually confirm the three insertions look correct (no broken braces, no duplicated lines, IIFE still closed properly).

- [ ] **Step 3: Build**

Run: `dotnet build Timinute.sln --configuration Release`
Expected: `Build succeeded`. (JS isn't compiled by MSBuild, but the file is referenced by Blazor's static-files pipeline; build catches gross referencing issues.)

- [ ] **Step 4: Commit**

```bash
git -C "D:\Projects\WebApps\Timinute" add Timinute/Client/wwwroot/js/theme-bootstrap.js
git -C "D:\Projects\WebApps\Timinute" commit -m "feat(theme): expose register/unregister on window.__theme

When the OS color scheme changes mid-session AND the user's stored
preference is 'System', call back into Blazor so ThemeService can fire
its Changed event. Topbar's stale-icon issue gets fixed in the next
commit by adding the JSInvokable target on the Blazor side."
```

---

## Task 5: `ThemeService.RegisterOsChangeListenerAsync` + `[JSInvokable]`

**Files:**
- Modify: `Timinute/Client/Services/ThemeService.cs`
- Modify: `Timinute/Client/Shared/MainLayout.razor`

- [ ] **Step 1: Add `IDisposable`, `selfRef` field, `RegisterOsChangeListenerAsync`, and `NotifyResolvedThemeChangedAsync` to ThemeService**

In `Timinute/Client/Services/ThemeService.cs`:

**1a)** Change the class declaration line. Use Edit with this old_string:

```csharp
    public class ThemeService
    {
```

And this new_string:

```csharp
    public class ThemeService : IDisposable
    {
```

**1b)** Add a `selfRef` field. Use Edit with this old_string:

```csharp
        private readonly IJSRuntime js;
        private readonly IHttpClientFactory clientFactory;
```

And this new_string:

```csharp
        private readonly IJSRuntime js;
        private readonly IHttpClientFactory clientFactory;
        private DotNetObjectReference<ThemeService>? selfRef;
```

**1c)** Add the two new methods AT THE END of the class, immediately before the closing `}` of the class. Use Edit with this old_string:

```csharp
        private async Task ApplyLocalCoreAsync(ThemePreference value)
        {
            try
            {
                await js.InvokeVoidAsync("__theme.set", value.ToString());
            }
            catch (JSException) { /* bootstrap script absent — non-fatal */ }
            catch (JSDisconnectedException) { /* circuit gone */ }
        }
    }
}
```

And this new_string:

```csharp
        private async Task ApplyLocalCoreAsync(ThemePreference value)
        {
            try
            {
                await js.InvokeVoidAsync("__theme.set", value.ToString());
            }
            catch (JSException) { /* bootstrap script absent — non-fatal */ }
            catch (JSDisconnectedException) { /* circuit gone */ }
        }

        // Register a callback so theme-bootstrap.js can notify us when
        // the OS color scheme changes mid-session for a 'System' user.
        // Idempotent — only the first call hits JS; later calls are no-ops.
        public async Task RegisterOsChangeListenerAsync()
        {
            if (selfRef != null) return;
            selfRef = DotNetObjectReference.Create(this);
            try { await js.InvokeVoidAsync("__theme.register", selfRef); }
            catch (JSException) { selfRef.Dispose(); selfRef = null; /* bootstrap absent */ }
            catch (JSDisconnectedException) { selfRef.Dispose(); selfRef = null; /* circuit gone */ }
        }

        // Invoked from theme-bootstrap.js when the OS color scheme changes
        // AND the user's stored preference is 'System'. The JS bootstrap
        // has already updated <html data-theme>; we just fire Changed so
        // Topbar re-renders its sun/moon icon.
        [JSInvokable]
        public Task NotifyResolvedThemeChangedAsync()
        {
            Changed?.Invoke(ThemePreference.System);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            selfRef?.Dispose();
            selfRef = null;
        }
    }
}
```

- [ ] **Step 2: Register the listener in `MainLayout.razor`'s `OnAfterRenderAsync`**

In `Timinute/Client/Shared/MainLayout.razor`, find this exact block (around lines 50-57):

```csharp
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // No `firstRender` guard: InitializeAsync is internally idempotent on success
        // and resets its `initialized` flag on failure, so calling it on every render
        // gives us a free retry path if the first JS import is racing prerender or
        // hot reload. The dedupe is cheap (a single bool check after the first hit).
        await Viewport.InitializeAsync();
```

Replace with:

```csharp
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // No `firstRender` guard: InitializeAsync is internally idempotent on success
        // and resets its `initialized` flag on failure, so calling it on every render
        // gives us a free retry path if the first JS import is racing prerender or
        // hot reload. The dedupe is cheap (a single bool check after the first hit).
        await Viewport.InitializeAsync();

        // Register the OS-color-scheme-change callback with the JS bootstrap.
        // RegisterOsChangeListenerAsync is internally idempotent (it short-circuits
        // once selfRef is set), so calling it every render is cheap.
        await Theme.RegisterOsChangeListenerAsync();


- [ ] **Step 3: Build**

Run: `dotnet build Timinute.sln --configuration Release`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Run the test suite (no new tests; just regression)**

Run: `dotnet test Timinute.sln --configuration Release --no-build`
Expected: 106 / 0 failed (unchanged from Task 3).

- [ ] **Step 5: Commit**

```bash
git -C "D:\Projects\WebApps\Timinute" add Timinute/Client/Services/ThemeService.cs Timinute/Client/Shared/MainLayout.razor
git -C "D:\Projects\WebApps\Timinute" commit -m "feat(theme): wire JS OS-change notification into ThemeService

ThemeService gains RegisterOsChangeListenerAsync + a [JSInvokable]
that fires the existing Changed event. MainLayout registers on first
render. Topbar's sun/moon icon now updates immediately when a System
user's OS color scheme flips, instead of waiting for the next render
trigger."
```

---

## Task 6: Create `UserProfileService` + DI registration

**Files:**
- Create: `Timinute/Client/Services/UserProfileService.cs`
- Modify: `Timinute/Client/Program.cs`

- [ ] **Step 1: Create the service**

Create `Timinute/Client/Services/UserProfileService.cs` with this exact content:

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Timinute.Client.Helpers;
using Timinute.Shared.Dtos;

namespace Timinute.Client.Services
{
    // Caches GET /User/me for the duration of the WASM app session.
    // Concurrent callers share the same in-flight Task; call
    // InvalidateAsync after any server mutation that changes the
    // cached fields (e.g. preferences PUT).
    public class UserProfileService
    {
        private readonly IHttpClientFactory clientFactory;
        private Task<UserProfileDto?>? fetchTask;

        public UserProfileService(IHttpClientFactory clientFactory)
        {
            this.clientFactory = clientFactory;
        }

        public event Action<UserProfileDto?>? Changed;

        public Task<UserProfileDto?> GetCurrentAsync()
            => fetchTask ??= FetchAsync();

        public Task InvalidateAsync()
        {
            fetchTask = null;
            return Task.CompletedTask;
        }

        private async Task<UserProfileDto?> FetchAsync()
        {
            try
            {
                var client = clientFactory.CreateClient(Constants.API.ClientName);
                var profile = await client.GetFromJsonAsync<UserProfileDto>("User/me");
                Changed?.Invoke(profile);
                return profile;
            }
            catch
            {
                // Unauthenticated, network error, or server hiccup — keep
                // the cache empty so the next call retries.
                fetchTask = null;
                return null;
            }
        }
    }
}
```

- [ ] **Step 2: Register in DI**

In `Timinute/Client/Program.cs`, find line 27:

```csharp
builder.Services.AddScoped<Timinute.Client.Services.ThemeService>();
```

Use Edit to insert ONE line BEFORE it:

```csharp
builder.Services.AddScoped<Timinute.Client.Services.UserProfileService>();
builder.Services.AddScoped<Timinute.Client.Services.ThemeService>();
```

(Order matters: UserProfileService is registered before ThemeService because ThemeService will gain a dependency on it in Task 7.)

- [ ] **Step 3: Build**

Run: `dotnet build Timinute.sln --configuration Release`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Run the test suite**

Run: `dotnet test Timinute.sln --configuration Release --no-build`
Expected: 106 / 0 failed.

- [ ] **Step 5: Commit**

```bash
git -C "D:\Projects\WebApps\Timinute" add Timinute/Client/Services/UserProfileService.cs Timinute/Client/Program.cs
git -C "D:\Projects\WebApps\Timinute" commit -m "feat(client): add UserProfileService — cached /User/me reads

New scoped service owns a Task<UserProfileDto?> cache. Concurrent
callers share the same in-flight request. Invalidation is explicit
(after PUT /User/me/preferences). Not used anywhere yet — wired up
in the next three commits."
```

---

## Task 7: Refactor `ThemeService` to consume `UserProfileService`

**Files:**
- Modify: `Timinute/Client/Services/ThemeService.cs`

- [ ] **Step 1: Add the `UserProfileService` dependency**

In `Timinute/Client/Services/ThemeService.cs`, find this block:

```csharp
        private readonly IJSRuntime js;
        private readonly IHttpClientFactory clientFactory;
        private DotNetObjectReference<ThemeService>? selfRef;
```

Replace with:

```csharp
        private readonly IJSRuntime js;
        private readonly IHttpClientFactory clientFactory;
        private readonly UserProfileService profileService;
        private DotNetObjectReference<ThemeService>? selfRef;
```

- [ ] **Step 2: Update the constructor**

Find:

```csharp
        public ThemeService(IJSRuntime js, IHttpClientFactory clientFactory)
        {
            this.js = js;
            this.clientFactory = clientFactory;
        }
```

Replace with:

```csharp
        public ThemeService(IJSRuntime js, IHttpClientFactory clientFactory, UserProfileService profileService)
        {
            this.js = js;
            this.clientFactory = clientFactory;
            this.profileService = profileService;
        }
```

- [ ] **Step 3: Remove the obsolete `syncTask` field**

Find:

```csharp
        // Holds the once-per-session sync Task so multiple callers (MainLayout,
        // Profile, Dashboard) don't all fire their own GET /User/me. First caller
        // populates; everyone else awaits the cached task. Profile's overload
        // (which already has the prefs) populates this with a completed task so
        // a later MainLayout call is a no-op.
        private Task<UserPreferencesDto?>? syncTask;
```

Delete the entire block (comment + field declaration), leaving the blank line above intact.

- [ ] **Step 4: Replace `SyncFromServerAsync()` (parameterless overload)**

Find:

```csharp
        public Task<UserPreferencesDto?> SyncFromServerAsync()
        {
            return syncTask ??= FetchAndApplyAsync();
        }
```

Replace with:

```csharp
        public async Task<UserPreferencesDto?> SyncFromServerAsync()
        {
            var profile = await profileService.GetCurrentAsync();
            if (profile?.Preferences != null)
            {
                await ApplyLocalCoreAsync(profile.Preferences.Theme);
                Changed?.Invoke(profile.Preferences.Theme);
                return profile.Preferences;
            }
            return null;
        }
```

- [ ] **Step 5: Simplify `SyncFromServerAsync(UserPreferencesDto)` (overload)**

Find:

```csharp
        // Server sync (overload): used when the caller already has the
        // server response, e.g. Profile.razor reusing its own GetMe. Also
        // populates the cache so a subsequent parameterless call (e.g. from
        // MainLayout) doesn't fire a duplicate GET /User/me.
        public async Task SyncFromServerAsync(UserPreferencesDto serverPrefs)
        {
            syncTask ??= Task.FromResult<UserPreferencesDto?>(serverPrefs);
            await ApplyLocalAsync(serverPrefs.Theme);
            Changed?.Invoke(serverPrefs.Theme);
        }
```

Replace with:

```csharp
        // Server sync (overload): used when the caller already has the prefs
        // (e.g. Profile.razor reusing its own UserProfileService fetch).
        // UserProfileService owns the cache now — this just applies locally
        // and notifies subscribers. The cache is already warm because
        // Profile's GetCurrentAsync populated it.
        public async Task SyncFromServerAsync(UserPreferencesDto serverPrefs)
        {
            await ApplyLocalCoreAsync(serverPrefs.Theme);
            Changed?.Invoke(serverPrefs.Theme);
        }
```

- [ ] **Step 6: Remove `FetchAndApplyAsync` (its work moved into `SyncFromServerAsync`)**

Find:

```csharp
        private async Task<UserPreferencesDto?> FetchAndApplyAsync()
        {
            try
            {
                var client = clientFactory.CreateClient(Constants.API.ClientName);
                var profile = await client.GetFromJsonAsync<UserProfileDto>("User/me");
                if (profile?.Preferences != null)
                {
                    await ApplyLocalCoreAsync(profile.Preferences.Theme);
                    Changed?.Invoke(profile.Preferences.Theme);
                    return profile.Preferences;
                }
            }
            catch
            {
                // Anonymous, network error, or server hiccup — keep cache.
                // Reset syncTask so a later, post-auth call can retry.
                syncTask = null;
            }
            return null;
        }
```

Delete the entire method.

- [ ] **Step 7: Invalidate after a successful PUT in `SetAsync`**

Find:

```csharp
            var response = await client.PutAsJsonAsync("User/me/preferences", dto);
            response.EnsureSuccessStatusCode();
        }
```

Replace with:

```csharp
            var response = await client.PutAsJsonAsync("User/me/preferences", dto);
            response.EnsureSuccessStatusCode();
            await profileService.InvalidateAsync();
        }
```

Use Edit. The literal `response.EnsureSuccessStatusCode();\n        }` should be unique within the file (Search for the SetAsync method specifically — there's only one `PutAsJsonAsync` call in this file).

- [ ] **Step 8: Build**

Run: `dotnet build Timinute.sln --configuration Release`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 9: Run the test suite**

Run: `dotnet test Timinute.sln --configuration Release --no-build`
Expected: 106 / 0 failed.

- [ ] **Step 10: Commit**

```bash
git -C "D:\Projects\WebApps\Timinute" add Timinute/Client/Services/ThemeService.cs
git -C "D:\Projects\WebApps\Timinute" commit -m "refactor(theme): consume UserProfileService for /User/me reads

ThemeService's syncTask field is gone — the cache moves to
UserProfileService. SyncFromServerAsync() routes reads through it;
SyncFromServerAsync(prefs) becomes a pure apply (cache already warm
from Profile's own fetch). SetAsync invalidates after a successful
PUT so the next read returns fresh data."
```

---

## Task 8: Refactor `Profile.razor` to use `UserProfileService`

**Files:**
- Modify: `Timinute/Client/Pages/Profile.razor`

- [ ] **Step 1: Add the `UserProfileService` `@inject` directive**

In `Timinute/Client/Pages/Profile.razor`, find the existing block of `@inject` lines near the top (currently lines 7-11). Add a new injection AFTER the `ThemeService` injection.

Use Edit with this old_string:

```razor
@inject Timinute.Client.Services.ThemeService ThemeService
@inject Radzen.NotificationService NotificationService
```

And this new_string:

```razor
@inject Timinute.Client.Services.ThemeService ThemeService
@inject Timinute.Client.Services.UserProfileService UserProfileService
@inject Radzen.NotificationService NotificationService
```

- [ ] **Step 2: Replace the direct HTTP call in `OnInitializedAsync`**

Find this block (around lines 149-165):

```csharp
    protected override async Task OnInitializedAsync()
    {
        var client = ClientFactory.CreateClient(Constants.API.ClientName);
        try
        {
            Me = await client.GetFromJsonAsync<UserProfileDto>("User/me");
            if (Me?.Preferences != null)
            {
                CurrentTheme = Me.Preferences.Theme;
                DraftWeeklyGoalHours = Me.Preferences.WeeklyGoalHours;
                DraftWorkdayHoursPerDay = Me.Preferences.WorkdayHoursPerDay;

                // Reuse this GetMe to seed the local theme cache rather than
                // letting MainLayout fire a duplicate request.
                await ThemeService.SyncFromServerAsync(Me.Preferences);
            }
        }
        catch
        {
            Me = null;
        }
```

Replace with:

```csharp
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Me = await UserProfileService.GetCurrentAsync();
            if (Me?.Preferences != null)
            {
                CurrentTheme = Me.Preferences.Theme;
                DraftWeeklyGoalHours = Me.Preferences.WeeklyGoalHours;
                DraftWorkdayHoursPerDay = Me.Preferences.WorkdayHoursPerDay;

                // Apply the theme locally; UserProfileService already holds
                // the cached profile so MainLayout won't re-fetch.
                await ThemeService.SyncFromServerAsync(Me.Preferences);
            }
        }
        catch
        {
            Me = null;
        }
```

Note that the `var client = ClientFactory.CreateClient(...)` line is also removed — Profile may still need `ClientFactory` for the later PUT path. Leave the `@inject IHttpClientFactory ClientFactory` directive intact (line 7).

- [ ] **Step 3: Build**

Run: `dotnet build Timinute.sln --configuration Release`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Run the test suite**

Run: `dotnet test Timinute.sln --configuration Release --no-build`
Expected: 106 / 0 failed.

- [ ] **Step 5: Commit**

```bash
git -C "D:\Projects\WebApps\Timinute" add Timinute/Client/Pages/Profile.razor
git -C "D:\Projects\WebApps\Timinute" commit -m "refactor(profile): use UserProfileService for /User/me read"
```

---

## Task 9: Refactor `Dashboard.razor` to use `UserProfileService`

**Files:**
- Modify: `Timinute/Client/Components/Dashboard/Dashboard.razor`

- [ ] **Step 1: Add the `UserProfileService` `@inject` directive**

In `Timinute/Client/Components/Dashboard/Dashboard.razor`, find the existing injection block at the top:

```razor
@inject IHttpClientFactory ClientFactory
@inject Timinute.Client.Services.ProjectColorService ColorService
```

Use Edit to add a new line BEFORE `ProjectColorService`:

```razor
@inject IHttpClientFactory ClientFactory
@inject Timinute.Client.Services.UserProfileService UserProfileService
@inject Timinute.Client.Services.ProjectColorService ColorService
```

- [ ] **Step 2: Replace the direct HTTP call**

Find this block (around lines 166-178):

```csharp
        try
        {
            var me = await client.GetFromJsonAsync<Timinute.Shared.Dtos.UserProfileDto>("User/me");
            if (me?.Preferences != null)
            {
                WeeklyGoalHours = me.Preferences.WeeklyGoalHours;
                WorkdayHoursPerDay = me.Preferences.WorkdayHoursPerDay;
            }
        }
        catch
        {
            // Keep the 32.0 / 8.0 fallbacks — progress bar still renders.
```

Replace with:

```csharp
        try
        {
            var me = await UserProfileService.GetCurrentAsync();
            if (me?.Preferences != null)
            {
                WeeklyGoalHours = me.Preferences.WeeklyGoalHours;
                WorkdayHoursPerDay = me.Preferences.WorkdayHoursPerDay;
            }
        }
        catch
        {
            // Keep the 32.0 / 8.0 fallbacks — progress bar still renders.
```

Only the one inner line changes (`client.GetFromJsonAsync<...>("User/me")` → `UserProfileService.GetCurrentAsync()`). Dashboard's other use of `client` (for `GetAllPagedAsync` on tasks) is unrelated and stays.

- [ ] **Step 3: Build**

Run: `dotnet build Timinute.sln --configuration Release`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Run the test suite**

Run: `dotnet test Timinute.sln --configuration Release --no-build`
Expected: 106 / 0 failed.

- [ ] **Step 5: Commit**

```bash
git -C "D:\Projects\WebApps\Timinute" add Timinute/Client/Components/Dashboard/Dashboard.razor
git -C "D:\Projects\WebApps\Timinute" commit -m "refactor(dashboard): use UserProfileService for /User/me read

With Profile and ThemeService both routed through UserProfileService,
every /User/me read in a session now hits the single cached Task —
hit count drops from 3-4 per session to 1."
```

---

## Task 10: End-to-end smoke (MANUAL)

**Files:** none — verification only.

This is a hand-off step. The implementer cannot drive a browser; the user runs through this checklist and reports results.

- [ ] **Step 1: Local SQL container running**

If the local SQL Server dev container isn't already up: `pwsh scripts/SetupDockerSql.ps1` from the repo root.

- [ ] **Step 2: Apply EF migrations (no schema change in this PR, but a safety check)**

Run: `pwsh scripts/MigrateDatabase.ps1`
Expected: "No migrations were applied. The database is already up to date." or successful idempotent apply.

- [ ] **Step 3: Start the app**

Run from the repo root: `dotnet run --project Timinute/Server/Timinute.Server.csproj`
Expected: app starts, listens on `https://localhost:7047`.

- [ ] **Step 4: Verify dedup of `/User/me` (item 4)**

Open `https://localhost:7047` in a browser. Sign in as `test1@email.com` / `123ABCabc!` (the seed password — confirm in `TestHelper.cs` if different).

In DevTools Network panel, filter for `User/me`. Navigate Dashboard → Tracked tasks → Profile → Dashboard again. Expected: **exactly one `GET /User/me` request** across all those navigations.

- [ ] **Step 5: Verify cache invalidation on prefs update (item 4)**

Stay logged in. Go to Profile. Change Weekly Goal from 32 to 40. Click Save. Navigate to Dashboard. Expected: dashboard weekly-goal progress bar uses 40, not 32. The Network panel shows a second `GET /User/me` (the cache was invalidated after the PUT).

- [ ] **Step 6: Verify OS-theme-change notification (item 3)**

In Profile, set theme to `System`. In the Topbar, note the current sun/moon icon (it shows the icon for the OPPOSITE of the resolved theme — moon when light is resolved, sun when dark is resolved). Now flip the OS color scheme:
- Windows: Settings → Personalization → Colors → "Choose your mode" → toggle Light ↔ Dark.
- Or in Chrome DevTools: Rendering panel → "Emulate CSS prefers-color-scheme" → toggle.

Expected: the Topbar icon flips within ~500ms without a page refresh. The dashboard surface colors also update (this was already working before this PR — it's the JS bootstrap re-applying `data-theme`).

- [ ] **Step 7: Verify GetMe aggregation (item 2)**

Sign in as `test1@email.com`. Open Profile. Expected: TaskCount, ProjectCount, TotalTrackedTime show the same values as before this PR (3 projects, 4 tasks, 14h totals for the seed user).

Optional: enable EF Core logging or inspect the SQL Server query log to confirm three small aggregate queries replace the previous SELECT-then-aggregate pattern.

- [ ] **Step 8: Verify StartDate breaking change is not user-visible (item 1)**

In TimeTracker, start the stopwatch, stop it, save the entry. Open Tracked Tasks list. Edit a task. Save. Expected: all paths work — the client always sends a real StartDate, so the non-nullable type doesn't surface as a UX change.

Try via raw `curl` (or HTTP client of choice) sending `POST /TrackedTask` with `"startDate": null` — expected: 400 / 422 with a model-validation error mentioning StartDate. (This is the "BREAKING" assertion in the commit message materializing.)

- [ ] **Step 9: Tear down**

`Ctrl+C` the `dotnet run`. Done.

Report back: ✅ all eight checks pass, or ❌ + which step failed + the relevant logs / screenshots.

---

## Task 11: Open the PR

**Files:** none

- [ ] **Step 1: Push the feature branch**

Run: `git -C "D:\Projects\WebApps\Timinute" push -u origin feature/p1-followups`

- [ ] **Step 2: Open the PR**

Run from `D:\Projects\WebApps\Timinute`:

```bash
gh pr create --base develop --repo jame581/Timinute --title "feat(p1): four review-raised follow-ups bundle" --body "$(cat <<'EOF'
## Summary

Closes four review-raised P1 follow-ups from the roadmap in a single bundled PR. Implements the design in `docs/superpowers/specs/2026-05-16-p1-followups-design.md`.

**Item 1 — `StartDate` DTO type fix** (Shared)
- `DateTimeOffset? StartDate` → `DateTimeOffset StartDate` on `CreateTrackedTaskDto` + `UpdateTrackedTaskDto`.
- `[Required]` retained for OpenAPI clarity; existing `EndDate > StartDate` + `MinDuration` validators catch missing-input cases.
- **BREAKING-CHANGE** for any future external API consumer: wire format changes from `"startDate": "2026-..." | null` to `"startDate": "2026-..."`.

**Item 2 — `GetMe` server-side aggregation** (Server)
- New `IRepository<T>.CountAsync(filter)` + `IRepository<T>.SumAsync(selector, filter)`.
- `UserController.GetMe` no longer materializes every task into memory — three SQL aggregates instead.
- 5 new unit tests against EF InMemory (`RepositoryAggregationTest.cs`).

**Item 3 — OS-theme-change notification** (Client + JS)
- `theme-bootstrap.js` gains `window.__theme.register/unregister`. The existing `matchMedia` listener now invokes a Blazor callback when the OS scheme flips AND the user is on `System`.
- `ThemeService` gains `RegisterOsChangeListenerAsync` + `[JSInvokable] NotifyResolvedThemeChangedAsync` + `IDisposable`.
- `MainLayout.razor` registers on first render. Topbar's stale-icon issue resolved.

**Item 4 — `UserProfileService`** (Client)
- New `UserProfileService` owns a `Task<UserProfileDto?>` cache (same syncTask pattern that lived in `ThemeService`).
- `ThemeService` now consumes it; `Profile.razor` and `Dashboard.razor` swap their direct HTTP calls for `UserProfileService.GetCurrentAsync()`.
- After `ThemeService.SetAsync`'s successful PUT, the cache is invalidated.
- Net effect: `/User/me` hit count drops from 3-4 per session to 1.

## Test plan

- [x] `dotnet build Timinute.sln --configuration Release` — Build succeeded, 0 errors
- [x] `dotnet test Timinute.sln --configuration Release --no-build` — 106 passed, 0 failed (101 existing + 5 new)
- [ ] Smoke: sign in, navigate Dashboard → Profile → Dashboard, DevTools Network panel shows exactly one `GET /User/me`
- [ ] Smoke: change weekly goal on Profile, return to Dashboard, new value shown (cache invalidated)
- [ ] Smoke: with theme = System, flip OS color scheme → Topbar icon updates within ~500ms
- [ ] Smoke: existing happy path for TimeTracker / Tracked tasks / Profile / Dashboard still works
EOF
)"
```

The `gh` CLI will print the PR URL on success.

- [ ] **Step 3: Confirm the PR opened**

`gh pr view --repo jame581/Timinute` should now show the new PR. Note the number for later.

---

## Out-of-scope notes (do not implement)

The following are explicitly out-of-scope per the spec. Do NOT pull them into this PR:

- Touching the `[Required]`-on-nullable-enum pattern for `Theme` (different concern from `StartDate`).
- Migrating `GetMe` to a dedicated stats endpoint (`/User/me/stats`).
- Promoting `ThemeService` to a generic user-state service via rename/merge.
- Real JavaScript unit tests for `theme-bootstrap.js`.
- Storing `Duration` as a `bigint` ticks column (separate schema-migration concern).
- New `int` or `decimal` overloads of `SumAsync` (YAGNI — `long` covers `Duration.Ticks`, the immediate need).
