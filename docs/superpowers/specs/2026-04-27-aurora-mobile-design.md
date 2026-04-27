# Aurora Mobile — Design Spec

**Status:** Approved for implementation. Aurora desktop redesign fully shipped 2026-04-26/27 (PRs #31–#34).
**Source of truth for visuals:** the Aurora handoff bundle (Claude Design output) — `aurora-mobile.jsx` (in-app), `aurora-mobile-landing.jsx` (landing), and the "Mobile (iOS) screens" section of `README.md`. The bundle is a local design artifact kept outside the repo (the originating workstation has it at `D:\Web\jame581\design_handoff_aurora\`); ask the project owner for a copy if you need to regenerate from source.
**This document:** project-specific decisions the visual handoff does not specify, plus a brief on-ramp for the implementer.

---

## 1. Scope

**In:** Mobile-responsive treatment of every existing in-app screen (Dashboard, Time tracker, Tracked tasks, Calendar, Projects, Profile, Trash) plus the landing page. New `MobileTabBar` (bottom 5-slot nav with center FAB) replaces the desktop sidebar at ≤768px. New `MobileMoreSheet` (slide-up overflow) for off-tab-bar destinations (Profile, Calendar, Trash, Log out). New `DayCalendar` component for the mobile calendar's day-view-with-week-strip layout.

**Out:**
- New backend endpoints, DTO changes, EF migrations.
- New design tokens. Mobile reuses every existing token from `aurora.css`; only sizing values shrink.
- The settings list card from `aurora-mobile.jsx`'s Profile screen (Notifications / Appearance / Goal per week / Export data). Skipped per Q3 — three of four rows would be visual-only and the fourth (Goal) requires a backend column. Defer until at least one row has real backing.
- The mobile-specific Dashboard topbar copy ("Hi, Jan / Friday April 24"). Existing route-driven topbar copy stays — small enough cost to keep one code path.
- Real GitHub star count on the landing's "Star on GitHub · 1.2k" link (already deferred from Aurora work).
- Dark-mode toggle, notification system, configurable weekly goal, data export — none of those are introduced; mobile just doesn't add new UI for them.

---

## 2. Architecture

### Viewport detection

A new `ViewportService` (scoped, registered in `Program.cs`) is the single source of truth for mobile-mode state.

**Mechanism.** On first render, `MainLayout` calls a small ES module at `wwwroot/js/viewport.js` that:

1. Reads `window.matchMedia('(max-width: 768px)').matches` once and pushes to .NET via a `[JSInvokable]` callback.
2. Subscribes to that media query's `change` event and pushes updates.

`ViewportService` exposes `bool IsMobile` and an `event Action OnChanged`. `MainLayout` subscribes, rerenders on change, and re-publishes via:

```razor
<CascadingValue Value="IsMobile" Name="IsMobile">
  …
</CascadingValue>
```

Components that need to branch on viewport read it via `[CascadingParameter(Name = "IsMobile")] bool IsMobile`. Most components don't need it — only `MainLayout`, `Topbar`, `TrackedTaskScheduler` (calendar host), and `MobileTabBar` itself.

The breakpoint `768px` matches the handoff. Single source of truth: `viewport.js` hardcodes the query; nothing else queries `matchMedia`.

### Component layout

| File | Status | Purpose |
|---|---|---|
| `Services/ViewportService.cs` | new | matchMedia → .NET bridge |
| `wwwroot/js/viewport.js` | new | small ES module, registered once |
| `Shared/MainLayout.razor` + `.razor.css` | edit | `IsMobile` cascade, conditional sidebar/tab-bar rendering |
| `Shared/Topbar.razor` + `.razor.css` | edit | mobile sizing via media query, mobile-only avatar trigger for `MobileMoreSheet` |
| `Shared/MobileTabBar.razor` + `.razor.css` | new | fixed-bottom 5-slot tab bar with FAB |
| `Shared/MobileMoreSheet.razor` + `.razor.css` | new | slide-up sheet for Profile / Calendar / Trash / Log out |
| `Services/MobileSheetService.cs` | new | scoped service exposing `Show()` / `Hide()` so Topbar's avatar can open the sheet |
| `Components/Scheduler/DayCalendar.razor` + `.razor.css` | new | mobile day-view calendar with week-strip selector |
| `Pages/TrackedTasks/TrackedTaskScheduler.razor` | edit | render `DayCalendar` if `IsMobile`, `WeekCalendar` otherwise |
| Per-screen `.razor.css` | edit | mobile reflow rules per Section 4 |
| `wwwroot/css/landing.css` | edit | extended `@media (max-width: 768px)` block |
| `Shared/NavMenu.razor` + `.razor.css` | unchanged | sidebar gated off at the layout level, not from inside |
| `wwwroot/index.html` | unchanged | boot splash already responsive |

### What stays untouched

Server, all controllers + DTOs, all repositories, EF migrations, Identity Razor Pages (login/register/manage all share the Aurora identity layout from PR #33; verify they look right at narrow viewports as part of smoke testing). All existing modals (`AddTrackedTask`, `AddTrackedTaskForm`, `EditTrackedTaskForm`, `AddProject`) — they already render in `RadzenDialog` which handles narrow viewports reasonably.

---

## 3. Layout shell on mobile

### `MainLayout.razor`

Authenticated branch:

```razor
<CascadingValue Value="IsMobile" Name="IsMobile">
  <AuthorizeView>
    <Authorized>
      <div class="aurora-app @(IsMobile ? "is-mobile" : "")">
        @if (!IsMobile)
        {
          <NavMenu />
        }
        <main class="aurora-main">
          <Topbar />
          <article class="aurora-content">@Body</article>
          @if (IsMobile)
          {
            <MobileTabBar />
            <MobileMoreSheet />
          }
        </main>
      </div>
    </Authorized>
    <NotAuthorized>@Body</NotAuthorized>
  </AuthorizeView>
</CascadingValue>
<RadzenDialog />
<RadzenNotification />
```

Unauthenticated branch (landing) keeps its bare `@Body` from phase 2 — the mobile landing reflow is pure CSS inside `landing.css`.

### `Topbar.razor`

Existing markup stays. Two changes:

1. **CSS sizing under `@media (max-width: 768px)`:** title 22px, subtitle 12.5px / single-line ellipsis, padding `6px 20px 14px`. Search bar + ⌘K hint chip hidden (the search is non-functional anyway). Quick Add button **also hidden** on mobile — the bottom tab bar's center FAB handles "+" actions everywhere, so a duplicate topbar button would just be visual noise.
2. **Mobile-only avatar trigger:** rendered only when `IsMobile` is true. Sits as the sole trailing element. 38×38 round, gradient background `linear-gradient(135deg, var(--accent), #9D7CFF)`, white initials. Click → `MobileSheetService.Show()`.

Topbar copy (route-driven titles + subtitles) stays the same on mobile.

### `MobileTabBar.razor`

Fixed-bottom positioning with 16px horizontal margin. Glass surface: `backdrop-filter: blur(20px) saturate(180%)`, white at 85% opacity, hairline border, soft shadow `0 12px 30px -12px rgba(20, 18, 40, .18)`. `padding-bottom: env(safe-area-inset-bottom, 0)` for iOS home indicator.

Layout: 5-column grid. Order: Home · Tracker · **+** FAB · Tasks · Projects.

- **Standard tab item.** Icon (21px, stroke 1.8) + label (10px / 500 / letter-spacing -0.1). Active = `var(--accent)` foreground. Inactive = `var(--text-mu)`. No background change.
- **Center FAB.** 44×44 rounded-rect (radius 14), `linear-gradient(135deg, var(--accent), #8B6FFF)`, white plus icon (22px, stroke 2.2), `0 8px 18px -4px rgba(91, 91, 245, .55)` shadow, `margin-top: -14px` so it pops above the bar. No label. Tap → `DialogService.OpenAsync<AddTrackedTask>` (same as desktop Quick Add).

Active-tab detection mirrors `NavMenu.IsActive` — strip query + fragment, normalize trailing slash, prefix-match against the route. Routes:
- Home → `/`
- Tracker → `/timetracker`
- Tasks → `/trackedtasks`
- Projects → `/projectmanager`

`backdrop-filter` fallback for browsers lacking support: solid `var(--surface)` at 95% opacity. Use the standard `@supports not (backdrop-filter: blur(1px))` guard.

The 100px content tail (added via `.aurora-content` mobile padding-bottom) prevents the tab bar from covering the last row of any list.

### `MobileMoreSheet.razor`

Hidden by default (`display: none`), shown when `MobileSheetService.IsOpen` is true. Slides up from the bottom of the viewport with a 200ms ease-out transform, dim full-screen backdrop above the tab bar.

Rows (in order, each tappable, each dismisses sheet on tap):
1. Profile → `/profile`
2. Calendar → `/scheduler`
3. Trash → `/trash`
4. Log out → existing `SignOutSessionStateManager.SetSignOutState` + `Navigation.NavigateTo("authentication/logout")`

Backdrop tap or row tap closes. Sheet has its own scoped CSS for the row layout (icon + label + chevron + hairline divider).

### `MobileSheetService`

Tiny scoped service:

```csharp
public class MobileSheetService
{
    public bool IsOpen { get; private set; }
    public event Action? OnChange;

    public void Show()
    {
        IsOpen = true;
        OnChange?.Invoke();
    }

    public void Hide()
    {
        IsOpen = false;
        OnChange?.Invoke();
    }
}
```

`Topbar.razor` injects it and calls `Show()`. `MobileMoreSheet.razor` subscribes to `OnChange` and rerenders.

---

## 4. Per-screen mobile reflows

All reflows are scoped CSS inside the existing component's `.razor.css` (or the page-level `.razor.css`). No markup changes except the calendar host.

| Screen | Reflow | Selector / approach |
|---|---|---|
| **Dashboard** | Hero card full-width, 22 padding, hero number 46px (down from 56). Stat row → 2-up. Bar chart full-width, 130px tall, 6 bars max-width 32px. Donut full-width below. Recent activity full-width. | Existing breakpoint at 1100px tightens; add ≤768 block |
| **TimeTracker** | Big timer 60px, centered. Action row centers with `justify-content: center`, gap 18: 50 reset · 78 play/pause · 50 edit. Project / Tags tiles drop to 2-up grid below the task tile. | `.aurora-tracker-card__top { flex-direction: column; align-items: stretch; }` plus action-row centering |
| **TrackedTasks** | Filter bar collapses to single search input + primary `+` button. Drop the 3 ghost buttons (All projects / date range / Export). Day-group rows stack: name on top, project pill + time-range as a meta line, mono duration right-aligned. | Existing `≤900px` rule extends; ghost buttons get `display: none` at ≤768 |
| **Projects** | Card grid → 1-column. Card pad 16. Sparkline 28px tall (down from 36). | Existing `≤720px` rule extends with sparkline tweak |
| **Profile** | 4-stat grid → 2×2. Identity row: avatar + meta stack vertically; ghost "Log out" + primary "Edit profile" stay side-by-side, full-width. | `grid-template-columns: repeat(2, 1fr)` + flex-direction column on header |
| **Trash** | Two cards stack. Each row: name on top, deleted-date / days-remaining as a meta line below, action buttons stacked or wrapped. | `.aurora-trash-row { grid-template-columns: 1fr; gap: 8px; }` |
| **Calendar** | Replace `WeekCalendar` with `DayCalendar` based on `IsMobile`. | Markup change in `TrackedTaskScheduler.razor` (only structural change of all reflows) |

### `DayCalendar.razor`

New component. ~150 lines of Razor + scoped CSS.

**Inputs (matches `WeekCalendar`'s contract):**
- `IList<TrackedTaskDto> Tasks` — full week's tasks
- `EventCallback<DateTime> OnEmptyClick`
- `EventCallback<TrackedTaskDto> OnEventClick`
- `DateTime WeekStart` — for the 7-day strip selector

**Internal state:** `DateTime SelectedDay` (defaults to today, falls back to `WeekStart` if today isn't in this week).

**Layout:**
1. **Week strip** — white card (pad 8, radius 18), 7 buttons across. Each button 8px vertical padding. Weekday label (10px uppercase) above day number (17px / 600). Active day = `var(--accent)` background, white text, accent glow shadow. Tap to switch `SelectedDay`.
2. **Day header** — left: "Today, Apr 24" + `N tasks · X.Xh` muted. Right: segmented `Day` / `Week` / `Month` (Day active). Week and Month buttons disabled with "Coming soon" tooltips, matching the desktop's deferred mode handling.
3. **Day grid card** (pad 0). 2-column grid: 44px hours column (8–18, mono labels) + 1fr events column. Hour rows 52px tall, hairline top borders. Events absolute-positioned, left/right 6px, top = `(hour - 8) * 52`, height = `max(duration_hours * 52, 28)`. Style: `background: <color>22; border-left: 3px solid <color>; border-radius: 8;`. Inner: title (11.5px / 500) + mono time-range (10px).
4. **Current-time line** (today only) — 2px tall danger line at the right top offset, with a 10px round dot on the left edge.

Click an empty cell → `OnEmptyClick` with the cell's `DateTime` (existing add modal). Click an event → `OnEventClick(task)` (existing edit modal).

`DayCalendar` ticks every 60s like `WeekCalendar` (current-time line refresh) and disposes its timer the same way.

`TrackedTaskScheduler.razor` host updates:

```razor
@if (IsMobile)
{
    <DayCalendar WeekStart="WeekStart" Tasks="WeekTasks"
                 OnEmptyClick="OnEmptyCellClicked" OnEventClick="OnEventClicked" />
}
else
{
    <WeekCalendar WeekStart="WeekStart" Tasks="WeekTasks"
                  OnEmptyClick="OnEmptyCellClicked" OnEventClick="OnEventClicked" />
}
```

Both consume the same `WeekTasks` list and the same prev/next/today navigation.

**Page toolbar on mobile.** The desktop's page-level toolbar (prev/today/next + range label + segmented Day/Week/Month + "Add task" primary button) is too cramped on a 402px viewport. On mobile, the page toolbar hides the segmented control and the "Add task" button — both responsibilities move into `DayCalendar` (segmented control in its day header) and the FAB (add-task) respectively. Prev/today/next + range label stay, since they navigate by week and the week strip alone doesn't cover that.

---

## 5. Mobile landing

Pure CSS reflow inside `wwwroot/css/landing.css`. No new components. Existing `LandingNav`, `LandingHero`, `LandingFeatures`, `LandingOss`, `LandingFooter` stay.

The current `landing.css` already has `@media (max-width: 1024px)` (hero stacks, features grid → 1col, OSS card stacks) and `@media (max-width: 640px)` (nav links collapse, type shrinks). Extend with a `@media (max-width: 768px)` block matching `aurora-mobile-landing.jsx`:

- **Top nav.** Hide `Features`, `Open source`, and `GitHub` text links. Keep wordmark + "Log in" pill. Drop the "Get started — free" CTA — the hero CTA carries it.
- **Hero.** H1 to 42px / weight 600 / letter-spacing -1.6 (down from 76). Lead paragraph 15px. Stacked full-width CTAs: primary "Register and start tracking →" first, then ghost "Star on GitHub · 1.2k". Trust strip stays single-row at 12.5px or wraps.
- **Hero card mock.** Padding 22, big mono timer 46px (down from 64), inset stat panel keeps the 12-bar sparkline.
- **Features.** Already stacks at 1024px; verify mini visuals still render at narrow widths.
- **OSS card.** Padding 24, headline wraps to 2 lines, side-by-side full-width buttons.
- **Footer.** Centered: mark + copyright row, links underneath.

Don't render the iPhone bezel — handoff says explicitly that's a prototype-only frame.

---

## 6. Build sequence

Single feature branch `feature/aurora-mobile` off `develop`. One PR back to `develop`. ~12–14 commits, each leaves the app buildable and the desktop experience unaffected:

1. **Spec doc** — commit this design doc.
2. **Viewport plumbing** — `Services/ViewportService.cs`, `wwwroot/js/viewport.js`, register in `Program.cs`. Nothing consumes it yet.
3. **MainLayout cascade** — add `[CascadingValue]`, subscribe to `ViewportService`. Sidebar still always renders; empty `MobileTabBar.razor` and `MobileMoreSheet.razor` stubs created so layout references compile.
4. **MobileTabBar implementation** — full glass tab bar markup + scoped CSS, FAB wired to `DialogService.OpenAsync<AddTrackedTask>`. Active-tab logic. Still hidden, no breakpoint gate.
5. **MobileMoreSheet implementation** — slide-up sheet markup + scoped CSS, `MobileSheetService` registered.
6. **Mobile shell wiring** — `MainLayout` gates `<NavMenu />` on `!IsMobile`, renders `<MobileTabBar />` + `<MobileMoreSheet />` when `IsMobile`. Topbar adds mobile avatar trigger + media-query reflow. App actually switches mode at 768px after this commit.
7. **DayCalendar component** — new component + scoped CSS. `TrackedTaskScheduler.razor` host renders `<DayCalendar />` if `IsMobile`, `<WeekCalendar />` otherwise.
8. **Per-screen reflow CSS** — one commit per screen for clean diffs:
   - 8a. Dashboard
   - 8b. TimeTracker
   - 8c. TrackedTasks
   - 8d. Projects
   - 8e. Profile
   - 8f. Trash
9. **Mobile landing reflow** — extend `landing.css` `@media (max-width: 768px)` block per Section 5.
10. **Smoke + cleanup pass** — verify boot splash, login, register on mobile viewport (Identity pages already responsive but worth confirming). Touch-target audit — every interactive element ≥ 44px square. Remove dead CSS uncovered.

---

## 7. Open questions / non-goals

- **Settings list card on Profile** — not in v1. Defer to a future PR when at least one row (likely Goal) has real backend backing.
- **Real GitHub star count** — already deferred from Aurora work; out of scope here.
- **Topbar mobile-specific copy on Dashboard** ("Hi, Jan / Friday April 24") — keep desktop copy; not worth a forked code path for v1.
- **iOS-style status-bar safe area at the top** — `env(safe-area-inset-top)` applied only to the tab bar's bottom (`safe-area-inset-bottom`). Top safe-area is handled by browser chrome on PWAs; if added later, attach to `.aurora-id-top` and `.aurora-topbar`.
- **Tablet treatment** — anything between 769–1023px gets the desktop layout. The handoff doesn't specify a tablet breakpoint, so we don't either. Revisit if it looks wrong.

---

## 8. Done

After this merges, every screen reflows below 768px to a coherent mobile experience: bottom tab bar, mobile-sized topbar, full-width cards, day-view calendar, slide-up overflow for off-bar destinations. Desktop experience unchanged. No backend changes. No new tokens.
