# Settings Cluster — Design Spec

**Status:** Approved for implementation. Brainstormed 2026-04-28 with the project owner; targets v2.1 on `develop`.
**Bundle:** Three roadmap items shipped together — Settings/Preferences (M), Dark-mode toggle (S), Configurable weekly goal (S). They share one user-preferences data model, so they land in one PR.
**This document:** the architecture and contracts. The implementation plan is generated separately by `writing-plans` after this spec is approved.

---

## 1. Scope

**In:**
- Owned EF Core entity `UserPreferences` on `ApplicationUser` with three fields: `Theme`, `WeeklyGoalHours`, `WorkdayHoursPerDay`.
- `GET /User/me` extended to include preferences.
- `PUT /User/me/preferences` (new, full-replace) with server-side validation.
- Dark-mode bootstrap: `wwwroot/js/theme-bootstrap.js` loaded synchronously in `<head>` to apply `data-theme` before Blazor mounts. Watches `prefers-color-scheme` for OS-theme changes when the user picks `System`.
- `ThemeService` (Blazor scoped) wraps the JS interop and the API call.
- New `Preferences` card on `Profile.razor` — three theme pills (instant) + two number inputs + Save button.
- `Dashboard.razor` switches from the hardcoded `WeeklyGoalHours = 32` to `Me.Preferences.WeeklyGoalHours`.

**Out (deferred):**
- Quick theme toggle in the topbar — Settings card is the only entry point in v2.1.
- Email-on-summary, week-start-day, time-format preferences — none has a current consumer.
- Authenticated-only theme scope (i.e. landing always-light) — we apply `data-theme` to `<html>` so dark mode covers landing too. Matches GitHub/Linear/Vercel behaviour and avoids the no-flash trade-off.
- Quick-toggle keyboard shortcuts.
- `WorkdayHoursPerDay` consumer wiring — the field is stored but no UI reads it. Forward-looking for the Enhanced Analytics P1.
- Granular per-field PUT or PATCH — full-replace PUT only.
- Row versioning / optimistic concurrency. Last-write-wins is fine for self-edited prefs.

---

## 2. Data model

`UserPreferences` is an EF Core *owned* entity on `ApplicationUser`. Owned-entity columns sit on the same table (`AspNetUsers`) with the default `Preferences_*` prefix.

```csharp
public class UserPreferences
{
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    [Column(TypeName = "decimal(4,1)")]
    public decimal WeeklyGoalHours { get; set; } = 32.0m;

    [Column(TypeName = "decimal(4,1)")]
    public decimal WorkdayHoursPerDay { get; set; } = 8.0m;
}

public enum ThemePreference { Light, Dark, System }
```

**EF config (in `ApplicationDbContext.OnModelCreating`):**

```csharp
builder.Entity<ApplicationUser>(b =>
{
    b.OwnsOne(u => u.Preferences, p =>
    {
        p.Property(x => x.Theme)
            .HasConversion<string>()
            .HasMaxLength(8)
            .HasDefaultValue(ThemePreference.System);

        p.Property(x => x.WeeklyGoalHours).HasDefaultValue(32.0m);
        p.Property(x => x.WorkdayHoursPerDay).HasDefaultValue(8.0m);
    });
});
```

**Why string-stored enum:** human-readable in SQL, no churn if we later add `AutoSchedule` or similar.

**Why `decimal(4,1)` (not `int`, not `double`):** users want half-hour precision (`7.5h` workday is a real European workweek of `37.5h`). 1 dp is the right granularity — 0.1h = 6 minutes, plenty fine; 2 dp would render as `8.00h` (false precision). `decimal` over `double` because SQL Server stores `7.5` as `7.5`, not `7.4999999`.

**Why owned entity (vs. columns on `ApplicationUser` directly):** logical grouping in code without a separate table. EF still emits one table, no joins. Easier to extend with more preferences later — no risk of preference fields scattering across `ApplicationUser` properties.

---

## 3. Migration

Single migration: `AddUserPreferences`.

**Up:**

```sql
ALTER TABLE AspNetUsers
    ADD Preferences_Theme               nvarchar(8)  NOT NULL DEFAULT 'System',
        Preferences_WeeklyGoalHours     decimal(4,1) NOT NULL DEFAULT 32.0,
        Preferences_WorkdayHoursPerDay  decimal(4,1) NOT NULL DEFAULT 8.0;
```

`defaultValue` on each column backfills existing rows in one statement — no separate UPDATE pass. Identity test seed users (`test1@email.com`–`test3@email.com`) inherit the defaults.

**Down:** drops the three columns. Reverse-scaffolded by EF; no data preservation needed since theme/goal are derivable preferences, not user content.

**No ApplicationUser seed-data churn:** the `Up` of `MigrateDateTimeToDateTimeOffset` (PR #37) seeded users with InsertData; we don't touch that — the new columns just take their defaults for those rows because EF's `defaultValue` applies to all existing rows including pre-seeded ones.

---

## 4. API contract

**One existing endpoint extended, one new.** No new controller — preferences are part of the user aggregate.

### 4.1 `GET /User/me` (existing, extended)

`UserProfileDto` gains a `Preferences` property:

```csharp
public class UserProfileDto
{
    // existing fields …
    public UserPreferencesDto Preferences { get; set; } = new();
}

public class UserPreferencesDto
{
    public ThemePreference Theme { get; set; }
    public decimal WeeklyGoalHours { get; set; }
    public decimal WorkdayHoursPerDay { get; set; }
}
```

### 4.2 `PUT /User/me/preferences` (new)

```
PUT  /User/me/preferences
Authorization: Bearer <jwt>
Content-Type: application/json

Body:
{
    "theme": "Dark",
    "weeklyGoalHours": 32.0,
    "workdayHoursPerDay": 8.0
}

200 OK → UserPreferencesDto (echoes the saved state)
422 Unprocessable Entity → validation errors (range, required) as ValidationProblemDetails
401 Unauthorized → no/invalid JWT
```

The 422 response is emitted by the global `InvalidModelStateResponseFactory` configured in `Server/Program.cs` (`ValidationProblemDetails` with `traceId` extension, `application/problem+json` content-type). The action's explicit `if (!ModelState.IsValid)` branch returns `UnprocessableEntity(ModelState)` to mirror the same status code if the action is invoked outside the standard `[ApiController]` pipeline (e.g. from a unit test that injects ModelState directly).

`UpdateUserPreferencesDto` — full-replace, all fields required:

```csharp
public class UpdateUserPreferencesDto
{
    // Nullable so [Required] actually rejects a missing JSON field —
    // on a non-nullable enum the binder substitutes the CLR default
    // (System) and validation passes silently, breaking the
    // "all fields required" contract.
    [Required]
    public ThemePreference? Theme { get; set; }

    [Required, Range(typeof(decimal), "1.0", "168.0")]
    public decimal WeeklyGoalHours { get; set; }

    [Required, Range(typeof(decimal), "0.5", "24.0")]
    public decimal WorkdayHoursPerDay { get; set; }
}
```

**Why PUT (full replace) and not PATCH:** with three fields, the client always knows the full state from the previous `GetMe`. PUT avoids the partial-update ambiguity flagged in the v2.0 review (`StartDate` `[Required]` on a nullable struct generated ambiguous OpenAPI output — that whole class of issue is sidestepped by full-replace).

**Enum wire format:** `JsonStringEnumConverter` is already configured server-side. Wire format is `"Light"`/`"Dark"`/`"System"` — matches the SQL `HasConversion<string>()` and is human-readable in Swagger.

**AutoMapper rules** (`MappingProfile.cs`): `UserPreferences ⇄ UserPreferencesDto` and `UpdateUserPreferencesDto → UserPreferences`. Both bidirectional / one-way as appropriate.

---

## 5. Dark-mode bootstrapping

The hardest UX requirement: **no flash of wrong theme on reload.** Blazor WASM takes ~1 sec to mount; if we wait that long to apply the user's theme, every page load flashes light-mode first.

### 5.1 Pre-Blazor bootstrap script

New file `Timinute/Client/wwwroot/js/theme-bootstrap.js`. Loaded synchronously in `<head>` of `index.html` *before* the Blazor framework script (so it runs first paint):

```js
(function () {
    const KEY = 'timinute:theme';
    const root = document.documentElement;

    function resolve(stored) {
        if (stored === 'Dark') return 'dark';
        if (stored === 'Light') return 'light';
        // 'System' (or unset): follow OS.
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    function apply(stored) { root.setAttribute('data-theme', resolve(stored)); }

    let stored;
    try { stored = localStorage.getItem(KEY) || 'System'; } catch { stored = 'System'; }
    apply(stored);

    // OS-theme change handler stays attached. The cur === 'System' guard
    // makes it a no-op once the user picks a fixed Light/Dark, so we don't
    // need to remove/re-add on toggle.
    const mql = window.matchMedia('(prefers-color-scheme: dark)');
    if (mql && mql.addEventListener) {
        mql.addEventListener('change', () => {
            let cur;
            try { cur = localStorage.getItem(KEY) || 'System'; } catch { cur = 'System'; }
            if (cur === 'System') apply('System');
        });
    }

    window.__theme = {
        set(stored) {
            try { localStorage.setItem(KEY, stored); } catch { /* private mode / quota */ }
            apply(stored);
        },
    };
})();
```

CSP-safe: a regular external script file, no `eval`, no inline JS in `index.html`.

### 5.2 Scope: app-wide

`data-theme` is set on `<html>`, not `.aurora-app`. This means the dark-mode tokens at `aurora.css:63` apply to the landing page too. Reasoning:

- Setting on `<html>` is the only no-flash path — `.aurora-app` doesn't exist until Blazor mounts.
- A user who's previously chosen Dark and is signed-out shouldn't get a blinding-white landing on reload.
- Industry practice (GitHub, Linear, Vercel) applies dark mode to landing/marketing pages too.

**Implementation note:** during build step 6, smoke-test the landing in dark mode. The hero gradient (`--gradient-hero` on `aurora.css:47`) and any `landing.css` values not pulled from `var(--*)` may need a small dark-mode pass. This is expected to be a handful of property tweaks, not a redesign — if it balloons, escalate.

### 5.3 Sync across devices

localStorage is a **cache** of the server's value, not the source of truth.

| Event | Local action | Server action |
|---|---|---|
| Page load (anonymous) | Bootstrap reads localStorage, applies theme | — |
| Page load (authenticated) | Same as above; then `App.razor` calls `GetMe`; if server's `Theme` differs, write it to localStorage and re-apply | `GET /User/me` |
| Toggle theme pill | `ThemeService.SetAsync(value)` — optimistic UI: `__theme.set(value)` immediately | `PUT /User/me/preferences` (full body); on failure: revert + toast |
| Sign-out | localStorage `timinute:theme` **NOT** cleared (per-browser preference, survives session) | — |
| Sign-in as different user | Next `GetMe` overwrites localStorage with that user's preference | — |

Cache-staleness window: at most one reload after a cross-device change (e.g., toggle on phone → reload on laptop catches up). Acceptable for prefs.

---

## 6. Client services

### 6.1 `ThemeService` (scoped, `Timinute/Client/Services/ThemeService.cs`)

Single point of contact for theme state. Owns the JS-interop + the API call.

```csharp
public class ThemeService
{
    public event Action<ThemePreference>? Changed;

    public Task<ThemePreference> GetCurrentAsync();              // reads localStorage via interop
    public Task SetAsync(ThemePreference value);                 // localStorage + PUT; raises Changed
    public Task SyncFromServerAsync(UserPreferencesDto server);  // called once after GetMe
}
```

Registered scoped in `Program.cs`. Components that show the current state (the Settings pill row) subscribe to `Changed`.

### 6.2 Server sync on app boot

In `App.razor` (or a small invisible cascading `ThemeBootstrap` component mounted at the root), after the user is authenticated and the first `GetMe` succeeds, call `ThemeService.SyncFromServerAsync(me.Preferences)`. This handles the cross-device sync — log in on a new device, theme catches up after the first authenticated request.

---

## 7. UI: Profile.razor — Preferences card

A new `<AuroraCard>` rendered below the existing Account+Stats card.

```
┌───────────────────────────────────────────────────────┐
│  Preferences                                          │
│  ───────────────────────────────────────────────────  │
│  Theme                                                │
│   ⚪ Light    ● Dark    ⚪ System                      │
│                                                       │
│  Weekly goal                                          │
│   ┌──────┐                                            │
│   │ 32.0 │ h                                          │
│   └──────┘                                            │
│                                                       │
│  Workday                                              │
│   ┌──────┐                                            │
│   │ 8.0  │ h                                          │
│   └──────┘                                            │
│                                                       │
│                          [ Save preferences ]         │
└───────────────────────────────────────────────────────┘
```

| Control | Behavior |
|---|---|
| Theme pills (Light/Dark/System) | Single-select. Click → `ThemeService.SetAsync(...)` immediately. Optimistic UI; on PUT failure, revert + toast. **Never gated on the Save button.** |
| Weekly goal | `<input type="number" step="0.5" min="1" max="168">`, suffix `h`. Local draft state only. |
| Workday | `<input type="number" step="0.5" min="0.5" max="24">`, suffix `h`. Local draft state only. |
| Save preferences | Sends the **current full state** (including the already-applied theme) via PUT. On 200 → success toast. On 4xx → field-level validation messages. On 5xx → error toast, draft preserved. |

**Why theme pills bypass the Save button:** there's no draft state for theme — the click *is* the choice. Users expect dark mode to apply immediately, not wait for a Save click. Save is for the numeric fields, where draft-and-commit is the natural pattern.

**`AuroraCard` reuse:** matches the existing card on the same page. New CSS lives in `Profile.razor.css` (already present, scoped).

---

## 8. Dashboard wiring

`Dashboard.razor:122-194` currently:

```csharp
private const int WeeklyGoalHours = 32;
private string WeeklyGoal => $"{WeeklyGoalHours}h";
ProgressPct = System.Math.Clamp((thisWeekHours / WeeklyGoalHours) * 100, 0, 100);
```

After this PR, `Dashboard` (which already calls `GetMe` for stats) reads `Me.Preferences.WeeklyGoalHours`. The constant goes away. Display formatting handles the now-decimal value: `Me.Preferences.WeeklyGoalHours.ToString("0.#")` to render `32` (no decimal) or `32.5` as appropriate.

`WorkdayHoursPerDay` — no current Dashboard consumer. The field is stored and round-trips through the API but no UI reads it in v2.1. The Enhanced Analytics P1 will be the first consumer (probably a "today vs target workday" indicator).

---

## 9. Error handling

| Failure mode | Behavior |
|---|---|
| `PUT /User/me/preferences` returns 4xx | Form stays in edit state, fields unchanged, error toast via `NotificationService` (matches `AddProject` pattern). |
| `PUT` returns 5xx / network error | Same as above — toast, no draft loss. |
| Theme pill click → PUT fails | Optimistic UI reverts: `ThemeService` re-applies the previous theme via `__theme.set(...)`, error toast. localStorage rewritten to the previous value too. |
| `theme-bootstrap.js` fails to load | `<html>` keeps no `data-theme` attribute → `:root` defaults (light tokens) apply. Blazor mount continues. App fully functional, just always-light until next reload. (Fail-open for a decorative concern.) |
| `localStorage` unavailable (private mode, quota exceeded) | Bootstrap's `try/catch` returns `null` → falls through to `'System'` → resolves via `matchMedia`. `__theme.set` silently swallows the `setItem` exception. Theme works for the session, just doesn't persist. |
| `matchMedia` unavailable (very old browser) | The bootstrap's `mql.matches` access returns falsy → resolves to light. No crash. The change-listener is gated on `mql.addEventListener` being defined. |
| `GetMe` fails on app boot | localStorage value remains authoritative for the session. Next successful `GetMe` will catch the cache up. |
| Concurrent edits across devices | No row versioning. Last-write-wins. Acceptable for self-edited prefs. |

---

## 10. Testing

### Server (`Timinute/Server.Tests`)

Existing setup: xUnit + Moq + EF InMemory + `ControllerTestBase<T>`.

| Test | Verifies |
|---|---|
| `UserController_GetMe_ReturnsPreferencesWithDefaults` | Newly created user → `GetMe` response includes `Preferences` with `Theme="System"`, `WeeklyGoalHours=32.0`, `WorkdayHoursPerDay=8.0`. |
| `UserController_UpdatePreferences_WithValid_Returns200AndPersists` | PUT happy path, then `GetMe` reflects the new values. |
| `UserController_UpdatePreferences_WeeklyGoalOutOfRange_Returns400` | `0.9` and `168.1` → 400. |
| `UserController_UpdatePreferences_WorkdayOutOfRange_Returns400` | `0.4` and `24.1` → 400. |
| `UserController_UpdatePreferences_InvalidTheme_Returns400` | Unknown enum string from the wire → 400 (binder + Required). |
| `UserController_UpdatePreferences_RequiresAuth` | No JWT → 401. Mirrors existing `UserController` auth tests. |

If a `MappingProfileTest` already exists, extend it with `UserPreferences ⇄ UserPreferencesDto` and `UpdateUserPreferencesDto → UserPreferences` cases. If not, skip — out of scope.

### Client

The codebase has no client test project today, and adding one for this feature is out-of-scope. We rely on:

- Manual browser testing of the eight scenarios listed below.
- Build & Test CI workflow (covers all server changes).

**Manual smoke checklist (run against a local build):**
1. Theme pills — each click switches theme instantly; network panel shows a `PUT /User/me/preferences`.
2. Page reload — no flash of wrong theme on first paint.
3. OS theme change while `Theme="System"` — app re-applies without reload.
4. Numeric inputs — Save button only commits on click; out-of-range value shows validation toast on submit.
5. Cross-device sync — toggle on browser A; sign in on browser B; theme catches up after the first `GetMe`.
6. Sign out and back in as a different user — theme reflects that user's preference.
7. Private-mode browser — toggle works for the session, doesn't persist (no console errors).
8. Dashboard — change weekly goal in Settings → return to Dashboard → progress bar reflects new goal.

---

## 11. Build sequence

Eight steps, each landable independently and green-buildable. Order chosen so each step compiles + tests cleanly without depending on later ones.

1. **Data model + migration.** New files: `Models/UserPreferences.cs`, `Models/ThemePreference.cs`. Modify: `ApplicationUser.cs`, `ApplicationDbContext.cs`. Run `dotnet ef migrations add AddUserPreferences`. Verify build clean + scaffolded SQL matches §3.
2. **DTOs + AutoMapper + GetMe extension.** New files: `Shared/Dtos/User/UserPreferencesDto.cs`, `UpdateUserPreferencesDto.cs`. Modify: `UserProfileDto.cs`, `MappingProfile.cs`, `UserController.GetMe`. Add test: `GetMe_ReturnsPreferencesWithDefaults`.
3. **PUT endpoint + tests.** Modify `UserController` with the new `[HttpPut("me/preferences")]` action. Add the five `UpdatePreferences_*` tests.
4. **`theme-bootstrap.js` + `index.html` wire-up.** New file: `wwwroot/js/theme-bootstrap.js`. Modify: `index.html` `<head>`. Smoke: hard-reload any route, `<html>` should have `data-theme` set on first paint.
5. **`ThemeService` (Blazor) + DI registration.** New file: `Services/ThemeService.cs`. Register scoped in `Program.cs`. Wire `App.razor` to call `SyncFromServerAsync` after the first authenticated `GetMe`.
6. **`Profile.razor` — Preferences card.** Modify: `Profile.razor`, `Profile.razor.css`. Smoke-test landing in dark mode and patch any non-token-driven values in `landing.css` if needed.
7. **`Dashboard.razor` — drop the hardcode.** Replace the const with `Me.Preferences.WeeklyGoalHours`. Verify: change goal in Settings → Dashboard reflects it.
8. **Roadmap cleanup.** `docs/superpowers/plans/feature-roadmap.md`: remove the "Real GitHub star count" P1 row + dep-graph entry (shipped in v2.0.1), bump "Last reviewed" date, add a "Settings cluster shipped (v2.1)" entry, update the maintenance note.

Each step is one logical commit. PR target: `develop`. The whole bundle ships as one PR.

---

## 12. Out of scope (YAGNI)

- Quick theme toggle in the topbar. Discoverability of the Settings card is acceptable for v2.1; revisit if users complain.
- Email-on-summary, week-start-day, time-format, locale.
- Adding `WorkdayHoursPerDay` consumer code. Field is stored but no UI reads it. Forward-looking for Enhanced Analytics.
- Authenticated-only theme scope. Landing follows the user's theme too. Reverse if users complain.
- Per-user localStorage keys (e.g. `timinute:theme:<userId>`). Only matters if multiple users share a browser without using profiles — vanishingly rare.
- A11y media query: `prefers-reduced-motion` already honored elsewhere; no new motion introduced here, so no new pass needed.
- Row versioning / optimistic concurrency on `UserPreferences`.

---

## 13. Estimated complexity

Per the roadmap: M (Settings) + S (Dark-mode) + S (Weekly goal). Combined surface:

- ~7 new files: migration, two DTOs (`UserPreferencesDto`, `UpdateUserPreferencesDto`), `ThemePreference.cs`, `UserPreferences.cs`, `ThemeService.cs`, `theme-bootstrap.js`.
- ~11 modified files: `ApplicationUser`, `ApplicationDbContext`, `UserController`, `MappingProfile`, `UserProfileDto`, `Profile.razor`, `Profile.razor.css`, `Dashboard.razor`, `index.html`, `Program.cs`, `feature-roadmap.md`.
- ~6 new server tests.
- 1 documentation update (this file moves to `done/` later, plus the roadmap touch in step 8).
