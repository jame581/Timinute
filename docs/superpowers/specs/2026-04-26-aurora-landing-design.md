# Aurora Landing — Phase 2 Design Spec

**Status:** Approved for implementation. Phase 1 (in-app UI) merged on 2026-04-26.
**Source of truth for visuals:** `D:\Web\jame581\design_handoff_aurora\aurora-landing.jsx` plus the "Landing page" section of the handoff README.
**This document:** project-specific decisions the visual handoff does not specify.

---

## 1. Scope

**In:** Replace `Components/LandingPart.razor` (rendered for unauthenticated users by `Pages/Index.razor`) with the Aurora landing page. Five sections — Top nav, Hero with live-timer mock, Features grid, Open-source CTA with terminal mock, Footer. Split into per-section components under `Components/Landing/`. Update `MainLayout` so the Aurora sidebar/topbar shell is bypassed for unauthenticated views (the landing page provides its own top nav and is full-width).

**Out:** Authenticated screens (already Aurora-styled in phase 1). Login/register pages (Identity Razor Pages, still on Bootstrap, deferred). Backend changes (none required). Tests (no new server-side behavior).

---

## 2. Architecture

### Layout escape

The phase-1 `MainLayout` always wraps `@Body` inside the sidebar/topbar shell. That's wrong for the landing page, which has its own top nav and full-bleed gradient.

**Change:** `MainLayout` becomes conditional on auth state. Authenticated users continue to see the full Aurora shell. Unauthenticated users get a bare `@Body` — `RadzenDialog` and `RadzenNotification` still render at the root for both branches.

This keeps everything routed through the existing default layout (`App.razor`'s `DefaultLayout="@typeof(MainLayout)"`) without splitting routes or introducing a second layout component.

### Component layout

| File | Status | Notes |
|---|---|---|
| `Shared/MainLayout.razor` | edit | Branch on `<AuthorizeView>`; bare `@Body` when not authenticated |
| `Components/LandingPart.razor` | rewrite | Composes the 5 section components |
| `Components/Landing/LandingNav.razor` | new | Wordmark + section anchors + Log in + primary CTA |
| `Components/Landing/LandingHero.razor` | new | Eyebrow chip + H1 with hand-drawn underline + intro + CTAs + trust strip + live timer mock card |
| `Components/Landing/LandingFeatures.razor` | new | Header row + 3-card grid (Dashboard / Calendar / Tracked tasks) with mini visuals |
| `Components/Landing/LandingOss.razor` | new | Gradient card with copy + buttons + macOS-style terminal mock |
| `Components/Landing/LandingFooter.razor` | new | Mark + copyright + GitHub / Buy me a coffee / License links |
| `Components/Landing/landing.css` | new | Page-scoped tokens, layout, and section styles |

**Why per-section components:** the page is ~360 lines of JSX; flattening into one Razor file would be hard to navigate. Mirrors phase 1's pattern of breaking screens into focused components.

### Static assets

Geist + Geist Mono are already loaded by `index.html` (phase 1). `aurora.css` tokens are also already global. Landing reuses both.

`AuroraIcons` (phase 1) handles the icon set referenced by the design (`I.github`, `I.arrowR`, `I.dashboard`, `I.calendar`, `I.list`, `I.check`).

---

## 3. Behavior decisions

- **Live timer mock**: `System.Timers.Timer` at 1s intervals, decorative only. Initial seconds = 1827 (per JSX, ~30 minutes in). Component is `IDisposable`. No persistence, no backend call.
- **Hand-drawn underline under "minutes"**: inline SVG path matching JSX exactly.
- **Mini visuals in feature cards**:
  - Card 1 (Dashboard): 12-bar bar chart, last bar accent, others 50% accent.
  - Card 2 (Calendar): 7×3 grid with specific cells highlighted in palette colors.
  - Card 3 (Tracked tasks): 4 horizontal "rows" with colored leading bar + grey bar of varying width.
- **Terminal mock**: literal `<pre>` block with the install commands; final line in `--success` color.
- **Star count**: hardcoded "1.2k" — decorative.
- **CTA destinations**:
  - "Get started — free" / "Register and start tracking" → `authentication/register`
  - "Log in" → `authentication/login`
  - "Star on GitHub" / "View on GitHub" / nav GitHub link / footer GitHub link → `https://github.com/jame581/Timinute`
  - "Read docs →" → repo README on GitHub
  - Footer "Buy me a coffee" → `https://www.buymeacoffee.com/jame581`
  - Footer "License (MIT)" → repo LICENSE on GitHub
- **Section anchors**: `#features` / `#open` for nav links — handled natively by browser since they're plain anchors.
- **Responsive**: per the handoff, page targets 1280px max-width and is responsive only down to ~1024px. Below that, hero stacks (grid → 1col), features grid → 1col, open-source card stacks. Everything else uses the existing tokens, so no new media queries beyond the breakpoints listed in the handoff README.

---

## 4. Build sequence

Single feature branch `feature/aurora-landing` off `develop`. One PR. Three commits expected:

1. **Layout escape + spec doc** — make `MainLayout` bypass the shell for unauthenticated users; commit this design doc.
2. **Landing sections + page rebuild** — all 5 components + `landing.css` + rewritten `LandingPart.razor`.
3. **Cleanup pass** — remove `wwwroot/img/DashboardLandingPage.png`, `CalendarLandingPage.png`, `TrackedTasksTable.png` (referenced only by the old landing); any other dead assets uncovered.

---

## 5. Open questions / non-goals

- **Star count integration**: hardcoded "1.2k". A future follow-up could fetch from GitHub's API; out of scope for v1.
- **Hero card "Pair: timer logic refactor" copy**: matches handoff. Keep verbatim.
- **CTA "Register and start tracking" arrow**: from `AuroraIcons.arrowR` — already part of phase 1 icon set.
- **Dark mode**: tokens defined in `aurora.css`, no toggle, deferred per phase-1 spec.

---

## 6. Done

Phase 2 closes the Aurora redesign. After this merges, every Timinute screen except login/register matches the design system. Login/register restyle (still Bootstrap) becomes an independent follow-up.
