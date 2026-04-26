# Aurora Redesign — Design Spec

**Status:** Approved for phase 1 implementation planning.
**Source of truth for visuals:** `D:\Web\jame581\design_handoff_aurora\` (especially `aurora-app.jsx` and `README.md`).
**This document:** captures the architectural and scoping decisions that the visual handoff does not specify.

---

## 1. Scope

**In scope (phase 1):** design tokens, layout shell, sidebar, topbar, and full UI rebuild of Dashboard, Time tracker, Tracked tasks, Calendar, Projects, Profile, Trash. Backend `Color` field on `Project` entity (with migration, DTOs, validation, color picker UI). Backend `CreatedAt` field on `ApplicationUser` (with migration; needed for Profile "Member since"). Light theme only.

**Out of scope (phase 1):** Landing page (separate spec, phase 2). Dark mode (defined in CSS but no toggle). Login/register pages (stay on Bootstrap). Search bar functionality, notification bell, calendar Day/Month modes (visually present, deferred).

---

## 2. Architecture

### CSS / fonts

- All Aurora tokens live in a new `Timinute/Client/wwwroot/css/aurora.css` as `:root` CSS variables. Loaded after Bootstrap and before component styles in `index.html`.
- Geist + Geist Mono loaded via `<link>` to Google Fonts in `index.html`.
- Bootstrap stays loaded (still used by login/register and error boundary). Inside redesigned screens, Aurora's scoped styles override.
- Old `wwwroot/css/app.css` rules tied to the old chrome (`.top-row`, `.nav-category-header`, etc.) get removed in the cleanup commit.

### Component layout

| File | Status | Notes |
|---|---|---|
| `Shared/MainLayout.razor` + `.razor.css` | rewritten | Two-pane shell, removes old top-row auth bar |
| `Shared/NavMenu.razor` + `.razor.css` | rewritten | Aurora sidebar (wordmark, sections, indicator bar, footer) |
| `Shared/Topbar.razor` + `.razor.css` | new | Title/subtitle + search + bell + Quick add |
| `Shared/AuroraIcons.razor` | new | Inline SVG icon set mirroring `I` from `shared.jsx` |
| `Shared/AuroraButton.razor` | new | Variants: primary, ghost, soft, sub |
| `Shared/AuroraCard.razor` | new | Generic card primitive |
| `Shared/ProjectPill.razor` | new | Color-aware project chip |
| `Shared/ProjectAvatar.razor` | new | Initials tile with project color |
| `Services/ProjectColorService.cs` | new | `GetColor(ProjectDto)` — uses `p.Color`, falls back to palette hash by `ProjectId` |
| `Components/Dashboard/Dashboard.razor` | rewritten | Three-row layout |
| `Components/Dashboard/WeekTimeBarChart.razor` | new | Inline-SVG bar chart, last 6 months |
| `Components/Dashboard/ProjectDonut.razor` | new | Inline-SVG donut, 160×160 |
| `Components/Dashboard/DoughnutChart.razor` | deleted | |
| `Components/Dashboard/ProjectColumnChart.razor` | deleted | |
| `Pages/TimeTracker.razor` | rewritten | New live-tracker card + today's sessions card |
| `Components/TrackedTasks/TrackTaskTime.razor` | refactored or deleted | Timer logic preserved (via existing `TimeTracker` service) |
| `Pages/TrackedTasks/TrackedTasks.razor` | rewritten | Filter bar + day groups; no Radzen grid |
| `Components/TrackedTasks/TrackedTaskTable.razor` | deleted | |
| `Pages/TrackedTasks/TrackedTasksManager.razor` | deleted | |
| `Pages/TrackedTasks/TrackedTaskScheduler.razor` | thin host | Mounts new `WeekCalendar` |
| `Components/Scheduler/WeekCalendar.razor` + `WeekCalendarEvent.razor` | new | Custom grid, replaces RadzenScheduler usage |
| `Components/Scheduler/AddTrackedTaskForm.razor` | unchanged | Still used by calendar empty-cell click |
| `Components/Scheduler/EditTrackedTaskForm.razor` | unchanged | Still used by calendar event click |
| `Pages/Projects/ProjectManager.razor` | rewritten | 3-column card grid; no Radzen grid |
| `Components/AddProject.razor` | extended | + 5-swatch color picker |
| `Pages/Profile.razor` | new | At `/authentication/profile`, replaces profile branch of `Authentication.razor` |
| `Pages/Authentication.razor` | trimmed | Profile branch removed; login/register branches stay (Bootstrap) |
| `Pages/Trash.razor` | restyled | Two cards (Projects / Tasks), rows mirror tracked-tasks pattern |
| `Components/LandingPart.razor` | unchanged in phase 1 | Replaced in phase 2 |

### What stays untouched

Server, all controllers + DTOs (except the `Color` add), all repositories, EF migrations except `AddProjectColor`, `RadzenDialog`, `RadzenNotification`, `UndoNotificationService`, `TrashPurgeService`, the existing `AuthenticationStateProvider` plumbing, all routes.

---

## 3. Backend changes

### 3a. `Color` field on Project

### Model

`Timinute/Server/Models/Project.cs`: add `public string Color { get; set; }` (max 7, format `#RRGGBB`). DB column nullable to allow migration backfill, but API enforces presence on create/update.

### Migration

`AddProjectColor` migration:
- Adds `Color nvarchar(7) NULL` column.
- `Up()` backfills existing rows in SQL: order by `ProjectId`, cycle through the 5 palette colors (`#6366F1`, `#F59E0B`, `#10B981`, `#EC4899`, `#94A3B8`).

### Default-on-create

`ProjectController.CreateProject` assigns a color before save when `dto.Color` is null/empty: round-robin based on the user's existing project count modulo 5, picking from the 5-color palette. User can override after creation via the edit modal.

### DTOs

`ProjectDto`, `CreateProjectDto`, `UpdateProjectDto` each gain `string Color { get; set; }`. AutoMapper picks up by name. On Create/Update DTOs add `[RegularExpression("^#[0-9A-Fa-f]{6}$")]`. Server returns 400 for malformed input.

### Color picker UI

`AddProject.razor` gets a row above the existing inputs:
- Label "Color" (Geist 11/500 uppercase).
- Five swatches in a row, each 28×28, `border-radius: 8px`. Selected swatch shows a white check icon (existing `AuroraIcons.Check` at 14px).
- No free-form hex input.

### 3b. `CreatedAt` field on ApplicationUser

`Timinute/Server/Models/ApplicationUser.cs`: add `public DateTimeOffset CreatedAt { get; set; }`. Set in the registration flow (Identity's `Register.cshtml.cs` or wherever new users are created — set to `DateTimeOffset.UtcNow` before `userManager.CreateAsync`).

Migration `AddUserCreatedAt`:
- Adds `CreatedAt datetimeoffset NOT NULL DEFAULT(SYSUTCDATETIME())` column.
- Existing rows default to migration-run timestamp (acceptable backfill — these are dev users; production isn't affected since this app isn't deployed).

Expose on a new `UserProfileDto` returned by a new `GET /api/User/me` endpoint (or extend an existing user endpoint if one exists). Client Profile page calls this to render the 4-stat grid.

### 3c. Client-side color fallback

`ProjectColorService.GetColor(ProjectDto p)`:
```
return !string.IsNullOrWhiteSpace(p.Color)
  ? p.Color
  : Palette[Math.Abs(p.ProjectId.GetHashCode()) % 5];
```
Used during the brief window between creation and round-trip, and as a defensive default. The service is registered as scoped in `Program.cs` (client `Program`).

---

## 4. Layout shell

### `MainLayout.razor`

```razor
<div class="aurora-app">
  <NavMenu />
  <main class="aurora-main">
    <Topbar />
    <div class="aurora-content">@Body</div>
  </main>
</div>
<RadzenDialog />
<RadzenNotification />
```

Container styles: `display: flex; height: 100vh; overflow: hidden; background: var(--bg);`. Sidebar `width: 240px; flex-shrink: 0;`. Main `flex: 1; overflow: auto;`.

The existing top-row auth bar (LoginDisplay + GitHub + Buy-me-a-coffee) is removed entirely. LoginDisplay's content moves into the sidebar footer's user row. GitHub link becomes the sidebar's "Star on GitHub" button. Buy-me-a-coffee is dropped from in-app chrome (planned for landing-page footer in phase 2).

### `NavMenu.razor`

Three regions inside one `<aside>`:

1. **Wordmark** (top, 24px top padding, 28px bottom padding): 30×30 gradient tile + `timinute` in Geist 600 18px (with the period in `var(--accent)`).
2. **Workspace section**: Dashboard, Time tracker, Tracked tasks, Calendar, Projects, **Trash**. (Trash placed here by user decision; not in the original handoff list.)
3. **Account section**: Profile, Manage.
4. **Footer** (margin-top: auto): "Open source. Free forever." card with a Star on GitHub button (links to `https://github.com/jame581/Timinute`); user row below with a 32px avatar (gradient `var(--accent)` to `#9D7CFF`), user's display name, plan label.

Active route detection: subscribe to `NavigationManager.LocationChanged` in `OnInitialized`, set state, `StateHasChanged()` on each tick. Active item gets `background: rgba(255,255,255,0.07)` and a `::before` indicator bar at `position: absolute; left: -14px; top: 8px; bottom: 8px; width: 3px; border-radius: 999px; background: var(--accent)`.

### `Topbar.razor`

Stateless. Reads route from `NavigationManager.Uri`, looks up title + subtitle in a static dictionary:

```csharp
private static readonly Dictionary<string, (string Title, string Subtitle)> RouteTitles = new()
{
  ["/"]                       = ("Dashboard",     "Welcome back, {0}. Here's your week so far."),
  ["/timetracker"]            = ("Time tracker",  "Hit play, get back to work."),
  ["/trackedtasks"]           = ("Tracked tasks", "Every minute you've logged."),
  ["/scheduler"]              = ("Calendar",      "See your week at a glance."),
  ["/projectmanager"]         = ("Projects",      "Keep your work organised."),
  ["/authentication/profile"] = ("User profile",  "Your account, your data."),
  ["/trash"]                  = ("Trash",         "Restore or permanently delete."),
};
```

The dashboard subtitle's `{0}` placeholder is filled with the user's first name (from `User.Identity.Name` or `given_name` claim, falls back to "" if unauthenticated). Default fallback for unrecognized routes: `(prevTitle ?? "Timinute", "")`.

Right cluster:
- **Search input** — visible, focused on `Ctrl/Cmd+K`. Typing does nothing in v1. Wired up via JS interop registered once in `OnAfterRenderAsync(firstRender)`.
- **Bell button** — hidden in v1 (`@if (false)`).
- **Quick add button** — `<AuroraButton Variant="primary" OnClick="OpenQuickAdd">+ Quick add</AuroraButton>`. `OpenQuickAdd` calls `DialogService.OpenAsync<AddTrackedTask>(...)` with the same args used by `Pages/TimeTracker.razor`.

---

## 5. Per-screen specifications

For all visual details (spacing, colors, fonts, sizes), see `aurora-app.jsx` and the README in the handoff folder. Decisions below capture only the C# / data-flow behavior the visual spec does not cover.

### Dashboard (`Components/Dashboard/Dashboard.razor`)

Three rows: hero stats (3 cards), charts (bar + donut), recent activity card.

- Hero "this week" card: derives `weekHours` client-side from existing dashboard endpoints; renders progress bar as `(weekHours / goal) * 100%` with `goal = 32` (hardcoded for v1; future: user-configurable).
- "Top project this month" card: from `ProjectDataItemsPerMonthDto` for current month, top 1 by hours, percent = `topHours / totalHours`.
- "Last month total" card: previous-month sum from `AmountWorkTimeByMonthDto`; chip color is danger if delta is negative.
- `WeekTimeBarChart`: takes `IList<MonthlyHours> data` (last 6 months), renders bars with the last bar styled differently (gradient fill + tooltip + bold X-label).
- `ProjectDonut`: takes `IList<ProjectSlice> slices`, renders SVG donut + legend on the right. Center text shows total hours.
- Recent activity: last 5 tracked tasks (descending start date) from existing tracked-tasks endpoint.

### Time tracker (`Pages/TimeTracker.razor`)

- Live tracker card: big timer rendered as three spans (`hh:mm`, `:`, `ss`) so the seconds span can be `var(--accent)` while the rest is `var(--text)`.
- Timer state: continue using existing `TimeTracker` service; UI ticks via `System.Timers.Timer` (1s interval) on this page only.
- Play/pause button: `var(--accent)` filled when stopped, `var(--danger)` filled when running, with `pulse` animation on the status dot.
- Reset button (52px round, ghost): zeros the seconds counter.
- Manual entry button: opens existing `AddTrackedTask` modal.
- Today's sessions card: tracked tasks where `StartDate.Date == Today.Date`, ordered desc.

### Tracked tasks (`Pages/TrackedTasks/TrackedTasks.razor`)

- No `RadzenDataGrid`. Render as plain `@foreach` over grouped tasks.
- Filter bar: text input (case-insensitive substring match over task name + project name), three ghost buttons that are visual-only for v1 (All projects, date range, Export).
- Day groups: client-side `GroupBy(t => t.StartDate.Date).OrderByDescending(g => g.Key)`. Empty groups skipped.
- Pagination: load all of the user's tasks for v1. Revisit with server-side paging if it becomes slow.
- Edit/play/more actions on each row: edit opens `EditTrackedTaskForm` modal; play starts a new tracker entry with the same name + project; more is a popup with delete (existing soft-delete flow).

### Calendar (`Components/Scheduler/WeekCalendar.razor` + `WeekCalendarEvent.razor`)

- Custom component, no Radzen scheduler.
- Inputs: `DateOnly weekStart` (Monday), `IList<TrackedTaskDto> tasks` (filtered to that week).
- Layout: CSS grid `grid-template-columns: 60px repeat(7, 1fr)` for header row + body. Body has hour rows of 56px from 8:00 to 18:00 (11 rows = 616px tall).
- Events: absolute-positioned `<div>` per task in its day's column. `top = (start.Hour + start.Minute/60.0 - 8) * 56px`; `height = task.Duration.TotalHours * 56px`. Out-of-range events clamp to visible bounds.
- Today's red current-time line: 2px tall absolute-positioned bar; only rendered when today is within the visible week. Updates every minute (Timer or `OnAfterRenderAsync` on a 60s interval).
- Click an empty cell → open `AddTrackedTaskForm` with the clicked datetime prefilled.
- Click an event → open `EditTrackedTaskForm` with that task.
- Toolbar: prev/today/next step one week. Day/Month buttons render but are non-functional in v1 (clicking them no-ops; tooltip "Coming soon").
- `TrackedTaskScheduler.razor` becomes a thin host that fetches tasks for the current week and mounts `WeekCalendar`.

### Projects (`Pages/Projects/ProjectManager.razor`)

- 3-column grid via CSS grid. Search bar filters cards client-side (case-insensitive name substring match).
- Each card uses `ProjectAvatar` (initials = first two non-whitespace characters of name, uppercase; falls back to one char if single-word).
- Subtitle: literal "Active" — drop the "· N collaborators" text since there's no team feature.
- Stat strip: "This month" hours = sum of project's tasks where `StartDate.Year == now.Year && StartDate.Month == now.Month`. "Sessions" = count of those tasks.
- Sparkline: 12 bars, one per month for the past 12 months. Bar height proportional to that month's hours within the project, normalized to the max month value across the 12.
- Existing `AddProject` modal handles new + edit (now with color picker).

### Profile (`Pages/Profile.razor`, route `/authentication/profile`)

- Single big card with avatar + name + email + "joined {date}". Avatar = `linear-gradient(135deg, var(--accent), #C77BFF)` background, white initials.
- 4-stat grid: total tracked time, project count, task count, member-since (`now - User.CreatedAt`, formatted as "{years}y {months}mo").
- Edit profile button: links to existing Identity profile flow (out-of-band Bootstrap UI for now).
- Logout button: existing Identity logout endpoint.

### Trash (`Pages/Trash.razor`)

- Keep existing data flow (`/api/Project/trash`, `/api/TrackedTask/trash`, restore, purge).
- Restyle: two cards stacked or side-by-side (responsive). Each card has the row pattern from Tracked tasks: project pill / item name / deleted-date / days-remaining / actions. Restore + permanent-delete buttons replace the play/edit/more cluster.
- No day groups — flat list per card, ordered by deletion date desc.
- Permanent-delete confirmation: keep existing `DialogService.Confirm` flow.

---

## 6. Build sequence (single feature branch off `develop`)

Branch: `feature/aurora-redesign`. Single PR back to `develop`. Each numbered item is ≥1 commit; every commit leaves the app buildable + runnable.

1. Backend changes — `Project.Color` (entity, migration, DTOs, AutoMapper, controller default, validation, color picker added to existing `AddProject.razor`) AND `ApplicationUser.CreatedAt` (entity, migration, set in registration, exposed via user-profile endpoint). Existing UI still works.
2. Aurora foundation — `aurora.css`, fonts, `AuroraIcons`, primitives (`AuroraButton`, `AuroraCard`, `ProjectPill`, `ProjectAvatar`), `ProjectColorService`. Nothing consumes them yet.
3. Layout shell — `MainLayout`, `NavMenu`, `Topbar`. Removes old top-row, GitHub link, Buy-me-a-coffee. Wires Quick add and ⌘K. Existing pages render inside the new shell with old content.
4. Dashboard — `Dashboard.razor`, `WeekTimeBarChart`, `ProjectDonut`. Delete `DoughnutChart.razor`, `ProjectColumnChart.razor`.
5. Time tracker — new `TimeTracker.razor`. Refactor or delete unused parts of `TrackTaskTime.razor`.
6. Tracked tasks list — new `TrackedTasks.razor`. Delete `TrackedTaskTable.razor`, `TrackedTasksManager.razor`.
7. Calendar — new `WeekCalendar.razor`, `WeekCalendarEvent.razor`. `TrackedTaskScheduler.razor` becomes thin host.
8. Projects — new `ProjectManager.razor` card grid.
9. Profile — new `Profile.razor`. Trim profile branch from `Authentication.razor`.
10. Trash restyle.
11. Cleanup — remove dead CSS (`.top-row.auth`, `.nav-category-header`, etc.), unused imports, dead components.

---

## 7. Open questions / deferred items

- **Pagination on Tracked tasks list**: load-all for v1. Revisit if performance is an issue.
- **Calendar Day/Month modes**: deferred. Buttons render but are no-ops.
- **Search bar functionality**: visual-only for v1.
- **Notification bell**: hidden for v1.
- **Dark mode toggle**: tokens defined, no UI toggle. Future sub-project.
- **Login/register pages restyle**: deferred. Bootstrap stays.
- **Landing page**: phase 2, separate spec.
- **Configurable weekly goal**: hardcoded `32h` for v1.
- **`User.CreatedAt` backfill**: existing users get the migration-run timestamp. Acceptable since the app isn't in production.

---

## 8. Non-goals

- No backend changes other than `Project.Color` and `ApplicationUser.CreatedAt`.
- No EF migrations other than `AddProjectColor` and `AddUserCreatedAt`.
- No changes to authentication, Identity Server config, or claim shape.
- No changes to soft-delete / trash plumbing.
- No new tests (this is a UI refactor; existing tests cover the unchanged backend).
