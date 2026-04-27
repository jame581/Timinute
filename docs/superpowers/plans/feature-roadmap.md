# Timinute Feature Roadmap

_Last reviewed: 2026-04-27 — after Aurora mobile (PR #35) merged into `develop`._

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

### UI / UX (Aurora design system, v2.0)
- **Design tokens** in `aurora.css` — surfaces, sidebar, accent (indigo), category palette, radii, shadows, fonts (Geist + Geist Mono).
- **In-app shell:** sidebar (desktop) / bottom tab bar with FAB (mobile) + slide-up overflow sheet for off-tab destinations.
- **Aurora-skinned screens:** Dashboard, Time tracker, Tracked tasks, Calendar, Projects, Profile, Trash, Login, Register.
- **Landing page:** redesigned hero with live decorative timer, feature grid, OSS CTA, terminal mock, footer.
- **Boot splash:** Aurora-themed `index.html` first-paint shell.
- **Responsive:** mobile breakpoint at ≤768px; landing also has a ≤480px tighter pass.
- **Accessibility:** `prefers-reduced-motion` honored on the `aurora-pulse` animation, `aria-current="page"` on active nav, `:focus-visible` outlines on interactive elements, `aria-modal` + Escape-to-close on the mobile sheet.

---

## Pending — P1 (Should-Have)

| Feature | Description | Complexity | Notes |
|---------|-------------|------------|-------|
| Tags / Labels | Tag entity, many-to-many to TrackedTask, UI for tagging on tasks + filter by tag | M | The design has tag pills (`focus`, `backend`) on the time tracker visually, but they're hardcoded |
| Settings / Preferences | User profile editable fields, configurable weekly goal (currently hardcoded `32h`), workday hours, dark-mode toggle | M | Mobile Profile spec deferred a settings list card waiting for this |
| Dark-mode toggle | Tokens already defined in `aurora.css` `[data-theme="dark"]`; needs UI toggle + localStorage persistence + initial system-preference detection | S | Cheapest path: settings card row → `[data-theme]` attribute on `<html>` |
| Configurable weekly goal | Backed-up column on `ApplicationUser`, settings UI, dashboard reads it | S | `Dashboard.razor:213` currently hardcodes `WeeklyGoalHours = 32` |
| Enhanced analytics | Custom date ranges, daily/weekly summaries, productivity trends, push aggregation server-side | M | Dashboard stats currently load all user tasks client-side |
| Notifications | Idle-time warnings, task reminders via SignalR or browser push | M | Topbar bell is hidden on desktop (visual placeholder only) waiting for this |
| Time tracking enhancements | Pomodoro timer, time estimates/goals, break tracking | L | None |
| Real GitHub star count | Replace landing's hardcoded `· 1.2k` with live fetch from GitHub API + localStorage cache | S | Deferred from Aurora work |

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
Settings ─┬─ Dark-mode toggle (UI)
          ├─ Configurable weekly goal
          └─ Team workspaces (P2)

Tags ─┬─ External-ticket integrations (P2)
      └─ AI categorization (P2)

Enhanced analytics — independent
Notifications — independent
Time tracking enhancements — independent
Real GitHub star count — independent
```

---

## Tech debt

Status reviewed 2026-04-27.

| Item | Status | Notes |
|------|--------|-------|
| Constants class growing large — split per domain | open | `Server/Helpers/Constants.cs` mixes role + claim + magic strings |
| Request/response logging middleware | open | Useful before production; not in critical path |
| DB indexes on UserId, ProjectId | open | Repository queries filter by these on every endpoint |
| Composite indexes for common analytics queries | open | Dashboard hits `GetTrackedTasks` per session |
| Unique constraint: project names per user | open | DB-level constraint + 409 handling on duplicate create |
| API versioning for future breaking changes | open | None of the API is published yet, so this is preparatory |
| Extract common DataGrid logic | ✅ moot | Aurora replaced `RadzenDataGrid` with custom row layouts; no shared grid logic remains |
| Move `<style>` blocks → scoped `.razor.css` | ✅ done | PR #34 |
| `:has()` browser-support fragility on landing nav | ✅ done | PR #35 review fix — switched to `[href*="github.com"]` |
| Per-render `IsActive` / `SelectedDayTasks` allocations | ✅ done | PR #35 review fix |

---

## How this doc is maintained

- New features start as a spec in `docs/superpowers/specs/`, optionally with an implementation plan in `docs/superpowers/plans/`.
- When a plan lands, move it to `docs/superpowers/plans/done/` and prepend a `Status:` block at the top with the merging PR + date.
- Update this roadmap (P1/P2 tables + Tech-debt) in the same PR that completes a feature, so the file always reflects what's currently in `develop`.
