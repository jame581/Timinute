# Timinute UI Redesign Specification

> **Status:** ⚠️ Superseded — this early scoping doc was replaced by the Aurora redesign specs and is preserved here for historical context only. The actual implemented design is documented across:
> - `docs/superpowers/specs/2026-04-26-aurora-redesign-design.md` — phase 1 (in-app shell + 5 screens + Profile + Trash)
> - `docs/superpowers/specs/2026-04-26-aurora-landing-design.md` — phase 2 (landing page)
> - `docs/superpowers/specs/2026-04-27-aurora-mobile-design.md` — mobile responsive treatment
>
> Aurora shipped via PRs #31 / #32 / #33 / #34 / #35 between 2026-04-26 and 2026-04-27.

## Current State Summary

- Blazor WASM with Radzen 10.x + Bootstrap 5
- Flexbox sidebar layout, single breakpoint at 640px
- Material theme, no dark mode
- Mixed form patterns (Bootstrap EditForm + Radzen TemplateForm)
- Open Iconic icons (limited set)
- Dashboard: 2x2 stat cards + column chart + donut chart

## Key Pain Points

1. **Inconsistent components** — Bootstrap EditForm in some places, Radzen in others
2. **Single responsive breakpoint** — misses tablet and large desktop
3. **No dark mode** or theme customization
4. **Limited mobile experience** — calendar height exceeds viewport, tables don't adapt
5. **Dashboard lacks depth** — no trends, no goals, no interactivity
6. **Accessibility gaps** — limited ARIA labels, no skip-to-content, inconsistent focus states
7. **Dated navigation** — no breadcrumbs, no collapsible panel menu, basic category headers

## Proposed Architecture

### Layout
```
Desktop (>=992px): Collapsible sidebar (240px/64px) + top bar (56px) + main content
Tablet (768-991px): Hidden sidebar + top bar with hamburger + main content
Mobile (<768px): Top bar + main content + optional bottom nav
```

### Theme System
- CSS custom properties for colors, spacing, typography, shadows
- `[data-theme="dark"]` selector for dark mode overrides
- Theme toggle in top bar, persisted to localStorage
- Material Design color palette (Blue primary, neutral grays)

### Navigation Redesign
- **RadzenPanelMenu** for collapsible sidebar sections
- **RadzenBreadcrumb** for page context
- **RadzenProfileMenu** for user dropdown (profile, settings, logout)
- Active state: left accent bar + highlighted background
- Touch targets: 44x44px minimum

### Form Standardization
- All forms use **RadzenTemplateForm** + **RadzenFormField** (Variant.Outlined)
- Consistent spacing via CSS variables
- Inline validation with **RadzenRequiredValidator**
- Full-width on mobile, max-width 600px container on desktop
- Submit + Cancel button pattern

### Dashboard Redesign
- Quick stats row: Total hours, avg/day, top project, streak
- Trend indicators (up/down arrows with percentage)
- Active projects table with click-to-drill-down
- Recent activity timeline (last 5 tasks)
- Interactive charts with tooltips
- Date range picker for custom periods

### Data Grid Enhancements
- Responsive column hiding on mobile
- **RadzenSplitButton** for row actions (Edit/Delete/Duplicate)
- Loading skeletons via **RadzenSkeleton**
- Meaningful empty states with CTA buttons
- Checkbox selection for bulk operations

### Radzen 10.x Components to Adopt
- RadzenSideBar, RadzenCard, RadzenSplitButton, RadzenPanelMenu
- RadzenFormField, RadzenProfileMenu, RadzenBreadcrumb
- RadzenSkeleton, RadzenBadge, RadzenTabs
- RadzenContextMenu, RadzenProgressBar

## Responsive Breakpoints
- xs: <576px (mobile portrait)
- sm: >=576px (mobile landscape)
- md: >=768px (tablet)
- lg: >=992px (desktop)
- xl: >=1200px (large desktop)
- xxl: >=1400px (extra large)

## Accessibility Goals (WCAG AA)
- Color contrast >=4.5:1 for normal text, >=3:1 for large text
- All interactive elements keyboard-focusable with visible indicators
- Semantic HTML (nav, main, article, button)
- ARIA labels on icon-only buttons
- Skip-to-content link
- prefers-reduced-motion support

## Implementation Phases

1. **Foundation** — CSS variables, theme toggle, layout refactor
2. **Navigation** — RadzenPanelMenu, breadcrumbs, profile menu
3. **Forms** — Standardize all forms to RadzenFormField pattern
4. **Dashboard** — Metric cards, trends, interactive charts
5. **Data Tables** — Responsive columns, split buttons, empty states
6. **Accessibility** — ARIA audit, keyboard nav, screen reader testing
7. **Polish** — Cross-browser testing, performance, user feedback
