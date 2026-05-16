# P1 Follow-ups Bundle — Design

_Spec date: 2026-05-16._

## Purpose

Clear four review-raised follow-up items from `docs/superpowers/plans/feature-roadmap.md` as a single bundled PR. None of the items is large on its own; bundling avoids four review cycles for ~200 LOC of total change.

The four items, all originally raised in earlier PR reviews:

1. **`StartDate` DTO contract** — `[Required] DateTimeOffset?` is ambiguous in OpenAPI / client-generator output. Make non-nullable.
2. **`UserController.GetMe` server-side aggregation** — currently loads every task into memory to compute totals/counts. Push the work to SQL.
3. **OS-theme-change notification for System users** — `theme-bootstrap.js` already re-applies `<html data-theme>` when `prefers-color-scheme` changes mid-session, but never notifies Blazor; the Topbar icon stays stale.
4. **Unified `UserProfileService`** — `Profile`, `Dashboard`, `MainLayout`, and `ThemeService` each fire their own `GET /User/me`. Only `ThemeService` caches.

## Decisions

| Axis | Choice |
|------|--------|
| PR shape | Single bundled PR off `feature/p1-followups` → `develop` |
| Item 1 — `StartDate` | `DateTimeOffset` non-nullable, keep `[Required]`; rely on existing `EndDate > StartDate` + `MinDuration` validators |
| Item 2 — aggregation | Add `CountAsync(filter)` + `SumAsync(selector, filter)` to `IRepository<T>`; translate to SQL `COUNT(*)` / `SUM(...)` |
| Item 3 — OS-theme | Add `window.__theme.register(dotnetRef)` + `[JSInvokable] NotifyResolvedThemeChangedAsync()` on `ThemeService`; fire existing `Changed` event |
| Item 4 — profile service | New `UserProfileService` owns the cache; `ThemeService` becomes a consumer; `Profile` + `Dashboard` + `MainLayout` all route through it |

## Architecture overview

### New files

| Path | Purpose |
|------|---------|
| `Timinute/Client/Services/UserProfileService.cs` | Caches `Task<UserProfileDto?>` (syncTask pattern). Exposes `GetCurrentAsync()`, `InvalidateAsync()`, and a `Changed` event. |
| `Server.Tests/Repository/RepositoryAggregationTests.cs` | Unit tests for `CountAsync` + `SumAsync` against EF InMemory. |

### Modified files

| Path | Change |
|------|--------|
| `Timinute/Shared/Dtos/TrackedTask/CreateTrackedTaskDto.cs` | `DateTimeOffset? StartDate` → `DateTimeOffset StartDate` |
| `Timinute/Shared/Dtos/TrackedTask/UpdateTrackedTaskDto.cs` | Same |
| `Timinute/Server/Repository/IRepository.cs` | Add `CountAsync` + `SumAsync` |
| `Timinute/Server/Repository/BaseRepository.cs` | Implement both |
| `Timinute/Server/Controllers/UserController.cs` | `GetMe` uses aggregation methods instead of in-memory loop |
| `Timinute/Client/wwwroot/js/theme-bootstrap.js` | Add `register`/`unregister`; call back to Blazor from `matchMedia` listener |
| `Timinute/Client/Services/ThemeService.cs` | Add `RegisterOsChangeListenerAsync` + `[JSInvokable] NotifyResolvedThemeChangedAsync`; delegate reads to `UserProfileService`; invalidate after `SetAsync` |
| `Timinute/Client/Shared/MainLayout.razor` | Call `Theme.RegisterOsChangeListenerAsync()` on first render |
| `Timinute/Client/Pages/Profile.razor` | Direct `GetFromJsonAsync` → `UserProfileService.GetCurrentAsync()` |
| `Timinute/Client/Components/Dashboard/Dashboard.razor` | Same |
| `Timinute/Client/Program.cs` | Register `UserProfileService` as `Scoped` (before `ThemeService`) |
| `Server.Tests/Controllers/UserControllerTests.cs` | `GetMe_*` tests stay valid (return values unchanged); internal path differs |
| `Server.Tests/**/*TrackedTask*Tests.cs` | DTOs constructed with `StartDate = null` get real values |

### Out of scope

- The `[Required]`-on-nullable-enum pattern for `Theme` (different concern from `StartDate`).
- Migrating `GetMe` to a dashboard-specific stats endpoint (`/User/me/stats`).
- Promoting `ThemeService` to a generic user-state service (rename / merge with `UserProfileService`).
- Real JavaScript unit tests for `theme-bootstrap.js` (no JS test infra in the repo today).
- Storing `Duration` as a `bigint` ticks column (separate schema-migration concern; bigger blast radius than this bundle deserves).

## Item 1 — `StartDate` DTO type fix

**Diff:**

```diff
     [Required]
-    public DateTimeOffset? StartDate { get; set; }
+    public DateTimeOffset StartDate { get; set; }
```

Applies identically to `CreateTrackedTaskDto` and `UpdateTrackedTaskDto`.

`[Required]` on a non-nullable struct is a no-op for MVC model binding, but it preserves the OpenAPI schema (`required: true`). The existing validators that catch missing/zero dates still apply:

- Controller's `EndDate > StartDate` check — fires when `EndDate` is reasonable and `StartDate` is `MinValue`.
- `[MinDuration]` on `Duration` — catches near-zero durations.

**Breaking change:** the wire format changes from `"startDate": "2026-..." | null` to `"startDate": "2026-..."`. No external API consumers exist today (the WASM client and server move in lockstep; the landing page does not hit `/TrackedTask`). The PR body will explicitly flag this for any future external integration.

**Test impact:** any test constructing `CreateTrackedTaskDto` or `UpdateTrackedTaskDto` with `StartDate = null` must use a real `DateTimeOffset` value. The implementation step that updates the DTOs also sweeps the test files in the same commit.

## Item 2 — Server-side aggregation in `GetMe`

### New repository methods

`IRepository<T>`:

```csharp
/// <summary>
/// Asynchronously counts entities matching the optional filter.
/// Honors EF global query filters (e.g. soft delete).
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

`BaseRepository<T>` implements both via EF Core's native `CountAsync()` / `SumAsync()` on `IQueryable`. The default `dbSet` already applies global query filters (soft delete), matching the existing `Get` behavior in `GetMe` — deleted tasks/projects do not count.

### `UserController.GetMe`

```csharp
var user = await userManager.FindByIdAsync(userId);
if (user == null) return NotFound();

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

Three SQL aggregates replace two `SELECT * FROM ... WHERE UserId = @p` materializations.

### Risk — `Duration.Ticks` SQL translation

`Duration` is `TimeSpan`, mapped to SQL Server `time(7)` by EF Core default. EF Core 8+ translates `TimeSpan.Ticks` for SQL Server (renders as `DATEDIFF_BIG` plus arithmetic). The expectation is that `SumAsync(t => t.Duration.Ticks, ...)` translates cleanly on .NET 10 / EF Core 10 against SQL Server 2025.

**If translation fails at runtime** (the EF Core support matrix can drift between minor versions), the fallback is:

```csharp
var totalTicks = (await taskRepository.Get<TimeSpan>(
    select: t => t.Duration,
    where:  t => t.UserId == userId)).Sum(d => d.Ticks);
```

The existing `Get<TType>(select, where)` has a `where TType : class` constraint that excludes `TimeSpan`. The fallback path therefore also requires relaxing that constraint OR introducing a sibling method without it. Both fixes are one-line; documented here so the implementer can pivot in a single commit if needed.

The smoke test in this bundle will validate the happy path against the real SQL Server container — that's the deciding signal.

### Tests added

`Server.Tests/Repository/RepositoryAggregationTests.cs` (new file, 5 tests):

- `CountAsync_WithFilter_ReturnsMatchingCount`
- `CountAsync_NoFilter_ReturnsTotal`
- `CountAsync_RespectsGlobalQueryFilter` (soft-deleted rows excluded)
- `SumAsync_WithFilter_ReturnsFilteredSum`
- `SumAsync_EmptySet_ReturnsZero`

`UserControllerTests.GetMe_*` existing assertions stay valid (response shape unchanged) — they implicitly verify the new aggregation path produces equivalent totals.

## Item 3 — OS-theme-change notification

### `theme-bootstrap.js`

Extend `window.__theme` with `register`/`unregister`, and fire the registered Blazor callback from the existing `matchMedia` listener (only when stored is `System`):

```diff
+    let dotnetRef = null;
+
     if (window.matchMedia) {
         const mql = window.matchMedia('(prefers-color-scheme: dark)');
         if (mql.addEventListener) {
             mql.addEventListener('change', function () {
                 let cur;
                 try { cur = localStorage.getItem(KEY) || 'System'; } catch { cur = 'System'; }
-                if (cur === 'System') apply('System');
+                if (cur === 'System') {
+                    apply('System');
+                    if (dotnetRef) {
+                        try { dotnetRef.invokeMethodAsync('NotifyResolvedThemeChangedAsync'); }
+                        catch { /* circuit gone */ }
+                    }
+                }
             });
         }
     }

     window.__theme = {
         set: function (stored) { /* unchanged */ },
         get: function () { /* unchanged */ },
         getResolved: function () { /* unchanged */ },
+        register: function (ref) { dotnetRef = ref; },
+        unregister: function () { dotnetRef = null; },
     };
```

The listener is already attached at IIFE time (before Blazor mounts). The only new behavior is forwarding the change to a registered Blazor callback. Users on `Light` or `Dark` see no behavior change — the existing `cur === 'System'` guard is unchanged.

### `ThemeService`

```csharp
public class ThemeService : IDisposable
{
    private DotNetObjectReference<ThemeService>? selfRef;

    public async Task RegisterOsChangeListenerAsync()
    {
        selfRef ??= DotNetObjectReference.Create(this);
        try { await js.InvokeVoidAsync("__theme.register", selfRef); }
        catch (JSException) { /* bootstrap absent */ }
        catch (JSDisconnectedException) { /* circuit gone */ }
    }

    // Invoked from theme-bootstrap.js when OS color scheme changes
    // AND the user's stored preference is 'System'. The JS bootstrap
    // has already updated <html data-theme>; we just fire Changed so
    // subscribers (Topbar) re-render.
    [JSInvokable]
    public Task NotifyResolvedThemeChangedAsync()
    {
        Changed?.Invoke(ThemePreference.System);
        return Task.CompletedTask;
    }

    public void Dispose() => selfRef?.Dispose();
}
```

`Topbar` already subscribes to `Changed` and re-reads `__theme.getResolved` to pick the right sun/moon icon — no Topbar changes required.

### `MainLayout.razor`

Register on first render, once per session:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        await Theme.RegisterOsChangeListenerAsync();
    }
    // existing SyncFromServerAsync logic stays
}
```

## Item 4 — `UserProfileService` + caller refactors

### New service

`Timinute/Client/Services/UserProfileService.cs`:

```csharp
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
            fetchTask = null;   // unauthenticated / network blip — retry on next call
            return null;
        }
    }
}
```

Scoped lifetime (per WASM app instance). Concurrent callers share the same in-flight `Task`. `InvalidateAsync` is the only way to force a re-fetch — called after `ThemeService.SetAsync` (preferences PUT) and any other future mutation that changes the profile shape.

### `ThemeService` refactor

`ThemeService` keeps `IJSRuntime` + `IHttpClientFactory` (for the PUT calls) and gains `UserProfileService` (for reads). Its private `syncTask` field is removed — the cache moves to `UserProfileService`. After `SetAsync`'s successful PUT, `ThemeService` calls `profileService.InvalidateAsync()` so the next read returns fresh data.

The two-overload `SyncFromServerAsync` API is preserved:

- `SyncFromServerAsync()` — reads via `UserProfileService.GetCurrentAsync()` and applies the theme.
- `SyncFromServerAsync(UserPreferencesDto)` — overload for callers (Profile) that already hold prefs; just applies locally. No longer populates a local cache (`UserProfileService` owns that).

`SetThemeOnlyAsync` is unchanged externally; internally it calls the new `SyncFromServerAsync` which routes through `UserProfileService`.

### Caller refactors

- **`Profile.razor:154`** — replace `Me = await client.GetFromJsonAsync<UserProfileDto>("User/me")` with `Me = await UserProfileService.GetCurrentAsync()`. The downstream `ThemeService.SyncFromServerAsync(Me.Preferences)` call stays unchanged.
- **`Dashboard.razor:168`** — replace `me = await client.GetFromJsonAsync<UserProfileDto>("User/me")` with `me = await UserProfileService.GetCurrentAsync()`. Same destructuring of `Preferences` follows.
- **`MainLayout.razor`** — no caller-level change. It already calls `Theme.SyncFromServerAsync()`, which now routes through `UserProfileService` internally.

After these three swaps, every read of `/User/me` for a given app session funnels through one cached `Task`. Hit count drops from 3–4 per session to 1.

### Tests added

`Server.Tests` is the only test project; the Client doesn't have unit tests today. The two service-level tests fit alongside it as plain xUnit + Moq:

- `UserProfileServiceTests.GetCurrentAsync_CachesAcrossCalls` — two `await`s, one mocked `HttpMessageHandler` response.
- `UserProfileServiceTests.InvalidateAsync_ForcesRefetch` — fetch, invalidate, fetch again → two HTTP responses.

These require referencing `Timinute.Client` from the test project (the Client assembly is a regular .NET class library aside from the WASM entrypoint, so this is straightforward — confirm at implementation time).

If the cross-project reference is awkward, fold these into the manual smoke checklist instead. The implementation plan picks one path.

## Manual verification (folded into the bundled smoke)

- DevTools Network panel: load Dashboard fresh → exactly one `GET /User/me`.
- Navigate Dashboard → Profile → Dashboard rapidly → still exactly one `GET /User/me`.
- Update weekly goal on Profile → return to Dashboard → new value displayed (cache invalidated by `ThemeService.SetAsync` → `profileService.InvalidateAsync()`).
- Flip OS color scheme with stored preference = System → Topbar sun/moon icon updates without manual refresh.
- (For item 2) Create a user with several hundred tracked tasks; load Profile → SQL profiler shows three small aggregates (`COUNT`, `COUNT`, `SUM`) instead of two full `SELECT *` table reads.
- Existing functional tests (101 today) plus the 5 new repository tests plus the 2 new service tests (if included) — all green.

## Risks & open questions

- **`Duration.Ticks` SQL translation**: assumed working on EF Core 10 + SQL Server. Fallback is documented above. Smoke decides.
- **Cross-project test reference for `UserProfileServiceTests`**: if Client → test reference is awkward, fold those two tests into manual smoke. The bundle still has 5 new repository tests as the test deliverable.
- **`StartDate` breaking change visibility**: no external consumers exist today, but the wire format does change. PR body flags it as a `BREAKING-CHANGE` line so anyone scanning history later spots it. A future API versioning effort (tech-debt list) would formalize this.
- **`UserProfileService` lifetime**: `Scoped` in WASM means per-app-session. After login, the cache is empty; first authenticated page populates it. After logout/login as different user, the WASM app does a full reload anyway — no cache-staleness concern.
- **`DotNetObjectReference` lifetime**: `ThemeService` is `Scoped` (per WASM app), so disposal lines up with app teardown. `IDisposable` implementation releases the ref defensively.
