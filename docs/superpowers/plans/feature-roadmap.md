# Timinute Feature Roadmap

_Last reviewed: 2026-07-12 — v2.2 shipped (PRs #46, #47); v2.3 scope defined (Enhanced analytics + leftover tech-debt sweep)._

## Current Feature Set

### Foundation
- **.NET 10** Blazor WebAssembly hosted app (Server + Client + Shared).
- **Auth:** Duende IdentityServer, registration via server-side Razor Page, JWT for the API, cookie scheme for Identity UI, Basic/Admin roles, lockout.
- **Data access:** Generic repository + factory pattern with paging, filtering, dynamic LINQ ordering, EF global query filter for soft delete.
- **Time/date:** all date columns + DTOs use `DateTimeOffset` (UTC normalization complete).
- **Validation:** Data Annotations on Create/Update DTOs incl. `MinDuration` for `TimeSpan`, `EndDate > StartDate` checks, hex-color regex on `Project.Color`. Ownership checks on every controller action.

### Core domain
- **Projects:** Full CRUD with user-defined Color, user-scoped, search + filter endpoint, paging.
- **Time tracking:** Full CRUD, real-time stopwatch with seconds-in-accent live timer, session-storage persistence across reload, manual entry, undo toast on delete.
- **Calendar:** Custom Aurora `WeekCalendar` (desktop, 7-day grid + current-time line) and `DayCalendar` (mobile, 7-day strip + day grid). Tap-to-add / tap-to-edit via existing modals.
- **Soft delete + Trash:** 30-day recovery for projects + tasks, cascade-restore on Project, `TrashPurgeService` background hard-purger.
- **Analytics:** Project work time, monthly aggregation, top-project stats, hand-built SVG bar chart + donut on the dashboard.
- **Search/filter:** `[HttpGet("search")]` on tasks (date range, project, name, task-count) and projects (name, min-task-count).
- **Data export:** CSV + Excel for tasks, project summaries, monthly analytics (`ExportController`).

### UI / UX (Aurora design system, v2.1)
- **Design tokens** in `aurora.css` — surfaces, sidebar, accent (indigo), category palette, radii, shadows, fonts (Geist + Geist Mono). Dark-mode token block at `[data-theme="dark"]` mirrored on the Identity Razor pages (`aurora-identity.css`).
- **In-app shell:** sidebar (desktop) / bottom tab bar with FAB (mobile) + slide-up overflow sheet for off-tab destinations.
- **Aurora-skinned screens:** Dashboard, Time tracker, Tracked tasks, Calendar, Projects, Profile (with Preferences card), Trash, Login, Register, OIDC redirect-back shells.
- **Landing page:** redesigned hero with live decorative timer, feature grid, OSS CTA, terminal mock, footer.
- **Boot splash:** Aurora-themed `index.html` first-paint shell — honors `data-theme="dark"`.
- **Responsive:** mobile breakpoint at ≤768px; landing also has a ≤480px tighter pass. Profile Preferences card uses a list-row treatment on mobile (label left, control right-aligned).
- **Dark mode:** `Light / Dark / System` preference, no flash on reload (synchronous pre-Blazor `theme-bootstrap.js`), localStorage cache + server source of truth, watches `prefers-color-scheme` for System users. Quick toggle in the desktop topbar (sun/moon icon).
- **User preferences:** owned EF entity `UserPreferences` on `ApplicationUser` (theme + weekly goal + workday hours). Dashboard hero card shows `Today X.Xh / Yh target` alongside weekly progress.
- **Accessibility:** `prefers-reduced-motion` honored on the `aurora-pulse` animation + boot/auth shells, `aria-current="page"` on active nav, `:focus-visible` outlines on interactive elements, `aria-modal` + Escape-to-close on the mobile sheet, `role="status" aria-live="polite"` on the auth in-flight shells.

---

## Planned — v2.3

v2.3 pairs the Enhanced analytics P1 feature with the tech-debt items that were scoped into v2.2 but did not ship (only the DB indexes + unique project-name constraint made it into PR #46).

### Feature

| Feature | Description | Complexity |
|---------|-------------|------------|
| Enhanced analytics | New `/analytics` page: custom date range + presets, server-side aggregation endpoints (per-day / per-project / per-tag), productivity trend chart vs `WorkdayHoursPerDay` target, per-tag breakdown (first analytics consumer of Tags). Dashboard retrofitted to the aggregate endpoints instead of loading all tasks client-side. | M |

### Tech debt to clear (carried over from v2.2)

- Build & Test workflow — registered on GitHub but `disabled_manually`; enable + verify a green run
- `Server/Helpers/Constants.cs` growing large — split per domain
- Request/response logging middleware
- API versioning for future breaking changes
- Server-side validation tests as integration tests

Per *How this doc is maintained*, Enhanced analytics gets a spec in `docs/superpowers/specs/` before implementation starts.

## Pending — P1 (Should-Have)

| Feature | Description | Complexity | Notes |
|---------|-------------|------------|-------|
| Notifications | Idle-time warnings, task reminders via SignalR or browser push | M | Topbar bell is hidden on desktop (visual placeholder only) waiting for this |
| Time tracking enhancements | Pomodoro timer, time estimates/goals, break tracking | L | None |

## Pending — P2 (Nice-to-Have)

| Feature | Description | Complexity | Dependencies |
|---------|-------------|------------|--------------|
| Team workspaces | Project sharing, team dashboards, task assignment | L | Settings |
| Calendar integration | Google Calendar / Outlook sync | L | None |
| Jira/GitHub integration | Link tasks to external tickets | L | Tags |
| Audit logging | Track all changes for compliance | M | None |
| Reporting suite | Scheduled email reports, custom report builder | L | Already-shipped Data Export |
| AI categorization | Automatic task categorization + productivity recommendations | L | Tags |

## Dependency Graph

```
Settings/Preferences ─── Team workspaces (P2)   [Settings cluster shipped in v2.1]

Tags ─┬─ External-ticket integrations (P2)      [Tags shipped in v2.2]
      ├─ AI categorization (P2)
      └─ Enhanced analytics per-tag breakdown (v2.3)

Enhanced analytics — v2.3 (consumes WorkdayHoursPerDay shipped in v2.1)
Notifications — independent
Time tracking enhancements — independent
```

---

## Tech debt

Status reviewed 2026-07-12.

| Item | Status | Notes |
|------|--------|-------|
| Build & Test workflow disabled | → v2.3 | Root cause found 2026-07-12: the workflow IS registered on GitHub (id 20059071) but its state is `disabled_manually`. Fix: `gh api -X PUT repos/jame581/Timinute/actions/workflows/20059071/enable`, then verify a run triggers on the next push/PR. CI on PRs currently only runs CodeQL + Copilot review. |
| Constants class growing large — split per domain | → v2.3 | `Server/Helpers/Constants.cs` mixes role + claim + magic strings |
| Request/response logging middleware | → v2.3 | Useful before production; not in critical path |
| DB indexes on UserId, ProjectId | ✅ done | Shipped in PR #46 (v2.2) — `IX_TrackedTasks_UserId`, `IX_TrackedTasks_ProjectId`, `IX_Projects_UserId` |
| Composite indexes for common analytics queries | ✅ done | Shipped in PR #46 (v2.2) — `IX_TrackedTasks_UserId_StartDate` |
| Unique constraint: project names per user | ✅ done | Shipped in PR #46 (v2.2) — filtered unique `IX_Projects_UserId_Name` (`[DeletedAt] IS NULL`) + 409 handling |
| API versioning for future breaking changes | → v2.3 | None of the API is published yet, so this is preparatory |
| Server-side validation tests as integration tests | → v2.3 | Current `UpdatePreferences_*OutOfRange*` tests inject `ModelState` errors directly; `[ApiController]` short-circuit path with the global 422 `InvalidModelStateResponseFactory` is not exercised. Raised by Copilot on PR #40 #6/#7. |
| Unified `UserProfileService` to dedupe `GET /User/me` | ✅ done | Shipped in PR #44 — `UserProfileService` owns a cached `GET /User/me`; ThemeService, Profile, and Dashboard now route through it (one read per session). |
| Extract common DataGrid logic | ✅ moot | Aurora replaced `RadzenDataGrid` with custom row layouts; no shared grid logic remains |
| Move `<style>` blocks → scoped `.razor.css` | ✅ done | PR #34 |
| `:has()` browser-support fragility on landing nav | ✅ done | PR #35 review fix — switched to `[href*="github.com"]` |
| Per-render `IsActive` / `SelectedDayTasks` allocations | ✅ done | PR #35 review fix |

---

## P1 follow-ups raised by reviews

| Feature | Description | Complexity | Source |
|---------|-------------|------------|--------|
| Direct-merge-to-develop policy | The soft-delete feature was merged direct via `271ffd7` without a PR (individually reviewed but no audit trail). Going forward, only housekeeping (templates, screenshots, tiny fixes) gets direct pushes; feature work goes through PR for the CI signal + reviewability. Status: followed since v2.0.1 — every feature ships via PR. | — | PR #37 release review M-3 (process, not code) |

## Recently shipped (v2.2, 2026-06-12)

| Feature | PRs |
|---------|-----|
| Tags / Labels — user-scoped `Tag` entity, implicit M2M to `TrackedTask` via `TaskTag`, `TagController` CRUD with 409 on duplicate name + force-delete, tag sync on task create/update, tag search filter, `/tags` management page, `TagPicker` component, filter chips on Tracked tasks, bUnit coverage. Tech debt: DB indexes (`UserId`, `ProjectId`, `UserId+StartDate`) + filtered unique `Project(UserId, Name)` constraint with 409 handling. Review hardening: split-query paging for include-heavy queries, whitespace-only tag-name rejection, `KnownIPNetworks` forwarded-header trust. | #46, release #47 |

## Recently shipped (post-v2.1, develop)

| Feature | PRs |
|---------|-----|
| Docker distribution — multi-stage image on `aspnet:10.0`, `docker-compose.yml` bundling SQL Server 2025, GHCR multi-arch (`linux/amd64` + `linux/arm64`) publish on `v*` tag and `develop` push, `docs/DOCKER.md` self-host guide. Includes production-hardening fixes surfaced during smoke: `ForwardedHeaders` moved to first in pipeline, Duende `KeyPath` set via `IdentityServerOptions` (not `KeyManagementOptions`), `SameSite=Lax` cookie policy, persistent ASP.NET data protection keys, all Docker-specific defaults pushed into `Dockerfile` `ENV` (not baked into `Program.cs`). | #43 |
| P1 review follow-ups bundle — `StartDate` made non-nullable on the TrackedTask Create/Update DTOs (with a `NonDefaultDateTimeOffsetAttribute` presence guard), `UserController.GetMe` aggregates project/task counts (`CountAsync` — server-side `COUNT`) and total tracked time (`SumAsync`) via new `IRepository<T>` methods, OS-color-scheme-change notification wired from `theme-bootstrap.js` into `ThemeService` (`RegisterOsChangeListenerAsync` + `[JSInvokable] NotifyResolvedThemeChangedAsync`), and a new `UserProfileService` caching `/User/me` so a session makes one read instead of 3-4. | #44 |
| P1 post-merge follow-up — fixes the `GET /User/me` 500 shipped by #44 (`SumAsync` no longer attempts an untranslatable SQL `SUM` over `TimeSpan.Ticks`; it materializes the projected column and sums in memory). Adds the `Timinute.Client.Tests` project (`UserProfileService` + `ThemeService` coverage) and `RepositoryAggregationSqliteTest` — a SQLite-backed test that catches SQL-translation bugs the EF InMemory suite cannot. | #45 |

## Recently shipped (v2.1, 2026-04-29)

| Feature | PRs |
|---------|-----|
| Settings / Preferences (owned EF entity, GET/PUT, Profile UI) | #40 |
| Dark-mode toggle (no-flash bootstrap, full app coverage incl. landing + Identity + auth shells) | #40 + #41 |
| Configurable weekly goal (decimal half-hour precision, Dashboard reads from preferences) | #40 |
| `WorkdayHoursPerDay` (stored in v2.1, consumed by Dashboard "Today vs target") | #40 + #41 |
| Quick theme toggle in desktop topbar | #41 |
| Mobile Profile prefs list-row CSS treatment | #41 |
| Branded auth shells (RemoteAuthenticatorView slots) | #41 |
| Real GitHub star count on landing | #38 (v2.0.1) |

## How this doc is maintained

- New features start as a spec in `docs/superpowers/specs/`, optionally with an implementation plan in `docs/superpowers/plans/`.
- When a plan lands, move it to `docs/superpowers/plans/done/` and prepend a `Status:` block at the top with the merging PR + date.
- Update this roadmap (P1/P2 tables + Tech-debt) in the same PR that completes a feature, so the file always reflects what's currently in `develop`.
