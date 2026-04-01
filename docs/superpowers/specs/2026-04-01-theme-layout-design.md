# Theme + Layout Modernization Design

## Goal

Add CSS variable-based theming with dark mode toggle, restyle sidebar and top bar from dark gradient to clean light/dark theme.

## Scope

- CSS custom properties for colors, spacing, borders
- Light and dark mode with `[data-theme]` attribute
- Restyle sidebar (light gray in light mode, dark in dark mode)
- Restyle top bar (white/dark with subtle shadow)
- ThemeToggle component (sun/moon icon button)
- Theme persistence in localStorage with flash prevention
- Dark mode option in user profile dropdown

Out of scope: RadzenPanelMenu migration, form standardization, dashboard redesign, responsive breakpoints, accessibility audit.

## Theme System

### CSS Variables (`variables.css`)

**Light mode (default):**
- `--color-bg-primary`: #ffffff
- `--color-bg-secondary`: #f5f5f5 (sidebar, cards)
- `--color-text-primary`: #212121
- `--color-text-secondary`: #666666
- `--color-border`: #e0e0e0
- `--color-shadow`: rgba(0,0,0,0.08)

**Dark mode (`[data-theme="dark"]`):**
- `--color-bg-primary`: #1e1e1e
- `--color-bg-secondary`: #2d2d2d
- `--color-text-primary`: #e0e0e0
- `--color-text-secondary`: #b0b0b0
- `--color-border`: #444444
- `--color-shadow`: rgba(0,0,0,0.3)

Accent colors stay as Radzen material defaults. Radzen components inherit via `--rz-*` variable overrides where needed.

### Flash Prevention

Inline script in `index.html` before Blazor loads:
```javascript
(function() {
    var theme = localStorage.getItem('theme') || 'light';
    document.documentElement.setAttribute('data-theme', theme);
})();
```

## Layout Restyle

### Sidebar (NavMenu)
- Remove dark gradient background
- Light mode: `var(--color-bg-secondary)` background, `var(--color-text-primary)` text
- Dark mode: inherits from CSS variables automatically
- Active nav item: 4px left accent bar (blue) + subtle background highlight
- Keep existing nav structure, Open Iconic icons, and category headers

### Top Bar (MainLayout)
- Light mode: `var(--color-bg-primary)` with bottom `box-shadow`
- Dark mode: same variables, shadow adjusts via `--color-shadow`
- Add ThemeToggle button on right side before user profile area

## ThemeToggle Component

New `Components/ThemeToggle.razor`:
- Button with Open Iconic icon: `oi-sun` (light mode) / `oi-moon` (dark mode)
- Clicks toggle `data-theme` on `document.documentElement` via JS interop
- Persists to `localStorage("theme")`
- Also added as "Dark mode" option in LoginDisplay dropdown

## Files to Create

- `Timinute/Client/wwwroot/css/variables.css`
- `Timinute/Client/Components/ThemeToggle.razor`

## Files to Modify

- `Timinute/Client/wwwroot/index.html` (link variables.css, flash prevention script)
- `Timinute/Client/wwwroot/css/app.css` (convert hardcoded colors to CSS variables)
- `Timinute/Client/Shared/NavMenu.razor.css` (restyle sidebar)
- `Timinute/Client/Shared/MainLayout.razor.css` (restyle top bar)
- `Timinute/Client/Shared/MainLayout.razor` (add ThemeToggle)
- `Timinute/Client/Shared/LoginDisplay.razor` (add dark mode option)

## Testing

No unit tests — purely CSS/UI with JS interop. Manual verification:
- Toggle dark/light mode on all pages
- Verify Radzen components inherit theme
- Verify theme persists across refresh
- Verify no flash of wrong theme on load
