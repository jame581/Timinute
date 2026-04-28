# Timinute Feature Roadmap

_Last reviewed: 2026-04-29 — after v2.1 release (Settings cluster + dark mode)._

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

## Pending — P1 (Should-Have)

| Feature | Description | Complexity | Notes |
|---------|-------------|------------|-------|
| Tags / Labels | Tag entity, many-to-many to TrackedTask, UI for tagging on tasks + filter by tag | M | The design has tag pills (`focus`, `backend`) on the time tracker visually, but they're hardcoded |
| Enhanced analytics | Custom date ranges, daily/weekly summaries, productivity trends, push aggregation server-side. First consumer of `WorkdayHoursPerDay` (stored in v2.1) beyond the Dashboard's "Today vs target" indicator. | M | Dashboard stats currently load all user tasks client-side |
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

Tags ─┬─ External-ticket integrations (P2)
      └─ AI categorization (P2)

Enhanced analytics — independent (consumes WorkdayHoursPerDay shipped in v2.1)
Notifications — independent
Time tracking enhancements — independent
```

---

## Tech debt

Status reviewed 2026-04-29.

| Item | Status | Notes |
|------|--------|-------|
| Build & Test workflow not registered | open | `.github/workflows/build_test.yml` is on master since `c57fa82` (PR #37) but `gh workflow list` doesn't show it as active and `gh run list --workflow=build_test.yml` returns 0 runs ever. CI on PRs only runs CodeQL. Investigate: YAML parse issue, or needs a `workflow_dispatch` to bootstrap registration. |
| Constants class growing large — split per domain | open | `Server/Helpers/Constants.cs` mixes role + claim + magic strings |
| Request/response logging middleware | open | Useful before production; not in critical path |
| DB indexes on UserId, ProjectId | open | Repository queries filter by these on every endpoint |
| Composite indexes for common analytics queries | open | Dashboard hits `GetTrackedTasks` per session |
| Unique constraint: project names per user | open | DB-level constraint + 409 handling on duplicate create |
| API versioning for future breaking changes | open | None of the API is published yet, so this is preparatory |
| Server-side validation tests as integration tests | open | Current `UpdatePreferences_*OutOfRange*` tests inject `ModelState` errors directly; `[ApiController]` short-circuit path with the global 422 `InvalidModelStateResponseFactory` is not exercised. Raised by Copilot on PR #40 #6/#7. |
| Unified `UserProfileService` to dedupe `GET /User/me` | open | Profile, Dashboard, MainLayout, ThemeService each currently hit GetMe. ThemeService has an internal cache; the others don't. Raised by `superpowers:code-reviewer` on PR #41 (M6). |
| Extract common DataGrid logic | ✅ moot | Aurora replaced `RadzenDataGrid` with custom row layouts; no shared grid logic remains |
| Move `<style>` blocks → scoped `.razor.css` | ✅ done | PR #34 |
| `:has()` browser-support fragility on landing nav | ✅ done | PR #35 review fix — switched to `[href*="github.com"]` |
| Per-render `IsActive` / `SelectedDayTasks` allocations | ✅ done | PR #35 review fix |

---

## P1 follow-ups raised by reviews

| Feature | Description | Complexity | Source |
|---------|-------------|------------|--------|
| `StartDate` DTO contract: `DateTimeOffset?` → `DateTimeOffset` | `[Required]` on a nullable struct generates ambiguous OpenAPI / client-generator output. Either drop `[Required]` and document partial-update semantics, or make the field non-nullable and rely on the controller-level `EndDate > StartDate` check for presence. Affects `CreateTrackedTaskDto` and `UpdateTrackedTaskDto`. Breaking change for any external consumer of the API. | S | PR #37 Copilot review |
| `UserController.GetMe` server-side aggregation | Currently pulls all of a user's tasks + projects into memory to compute totals/counts. Add `CountAsync(filter)` + `SumAsync(filter, selector)` to `IRepository<T>` (or expose `IQueryable`) and use them. Scales to users with thousands of tasks. | M | PR #37 Copilot review |
| Direct-merge-to-develop policy | The soft-delete feature was merged direct via `271ffd7` without a PR (individually reviewed but no audit trail). Going forward, only housekeeping (templates, screenshots, tiny fixes) gets direct pushes; feature work goes through PR for the CI signal + reviewability. Status: followed since v2.0.1 — every feature ships via PR. | — | PR #37 release review M-3 (process, not code) |
| OS-theme-change notification for System users | The pre-Blazor `theme-bootstrap.js` re-applies `<html data-theme>` when `prefers-color-scheme` changes mid-session, but doesn't notify Blazor — Topbar's icon stays stale until the next render. Wire `theme-bootstrap.js` `register(dotnetRef)` + `[JSInvokable] NotifyResolvedThemeChanged()` on `ThemeService`. ~30 LOC. | S | PR #41/#42 Copilot review (M2) — deferred from v2.1 by project owner |

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
