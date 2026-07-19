# Timinute

[![Release](https://github.com/jame581/Timinute/actions/workflows/release.yml/badge.svg)](https://github.com/jame581/Timinute/actions/workflows/release.yml)
[![Latest release](https://img.shields.io/github/v/release/jame581/Timinute)](https://github.com/jame581/Timinute/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/ghcr.io-jame581%2Ftiminute-blue?logo=docker)](https://github.com/jame581/Timinute/pkgs/container/timinute)

The free, open-source time tracker that respects your minutes. Track work hours across projects, see exactly where your time goes, and get clear weekly overviews — all in a self-hostable Blazor WebAssembly app.

Originally a demo of modern Blazor; now fully redesigned around the **Aurora** visual system (v2.1) with a tokenized design system, hand-built SVG charts, a custom calendar, full mobile-responsive treatment, and a no-flash dark mode that follows your system or your preference.

## Highlights

- **Time tracker** with a real-time stopwatch, session-storage persistence across reload, and undo on delete.
- **Calendar** — desktop week view with a current-time line, mobile day view with a 7-day strip selector. Click an empty cell to add, click an event to edit.
- **Projects** with user-picked colors, monthly stats, and per-project sparklines.
- **Tags** — user-scoped labels on tasks with a management page, inline tag picker in the task modals, and filter chips on the task list.
- **Dashboard** — gradient hero stat card, top-project + last-month tiles, hand-built SVG bar chart and donut, recent activity list.
- **Analytics** — dedicated page with date presets and a validated custom range: daily/weekly trend chart against your workday target, project donut, and per-tag breakdown, all served by range-scoped aggregate endpoints with client-side caching.
- **Trash** — 30-day soft-delete recovery for projects and tasks, cascade-restore on Project, background hard-purge service.
- **Search + filter** — `TrackedTask/search` (date range, project, name, task-count) and `Project/search` (name, min-task-count).
- **Data export** — CSV and Excel exports for tasks, project summaries, and monthly analytics.
- **AI / MCP** — a Model Context Protocol server at `/mcp` lets an AI assistant read your projects, time entries, and analytics (and, with a read-write token, log time) using a scoped personal access token; every call is recorded to a user-viewable AI activity log.
- **Identity** — Duende IdentityServer for auth (JWT for the API, cookie for Identity UI), Basic/Admin roles, lockout, registration via Razor Pages.
- **Mobile responsive** — bottom glass tab bar with FAB, slide-up overflow sheet, full reflow at ≤768px.
- **Dark mode** — `Light / Dark / System` preference, no flash on reload (synchronous pre-Blazor bootstrap), watches `prefers-color-scheme` for System users. Quick toggle in the desktop topbar.
- **User preferences** — configurable weekly goal and workday-hours target persisted server-side per user; the Dashboard hero card consumes both for weekly progress and a "Today X.Xh / Yh target" indicator.
- **Accessibility** — `prefers-reduced-motion` honored, `aria-current` on active nav, focus-visible outlines, modal sheet semantics.

## Screenshots

### Landing
![Landing page](screenshots/landing_page_screenshot.jpeg)

### Login
![Login page](screenshots/login_page_screenshot.jpeg)

### Dashboard
![Dashboard](screenshots/dashboard_page_screenshot.jpeg)

### Time tracker
![Time tracker](screenshots/timetracker_page_screenshot.jpeg)

### Tracked tasks
![Tracked tasks](screenshots/trackedtask_page_screenshot.jpeg)

### Calendar
![Calendar](screenshots/calendar_page_screenshot.jpeg)

### Projects
![Projects](screenshots/project_page_screenshot.jpeg)

## Tech stack

.NET 10 · Blazor WebAssembly (hosted) · EF Core 10 · SQL Server · Duende IdentityServer · Radzen.Blazor (dialogs/notifications only — design system is custom Aurora) · xUnit + Moq + bUnit (EF InMemory + SQLite test providers).

## Prerequisites

- [Visual Studio 2022 (17.x or newer)](https://visualstudio.microsoft.com/) or another .NET 10 IDE
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet)
- [Docker Desktop](https://www.docker.com/get-started) — used for the local SQL Server 2025 container

## Getting Started

```powershell
# 1. clone
git clone https://github.com/jame581/Timinute.git
cd Timinute

# 2. start a local SQL Server 2025 container on port 44555
.\scripts\SetupDockerSql.ps1

# 3. apply EF Core migrations
.\scripts\MigrateDatabase.ps1

# 4. run the app (server hosts the WASM client)
dotnet run --project Timinute/Server/Timinute.Server.csproj
```

Default URLs: <https://localhost:7047> / <http://localhost:5047>. Swagger lives at `/swagger`.

> **DB password:** a single environment variable, `MSSQL_SA_PASSWORD`, drives the SQL Server SA password everywhere — the local dev container (`SetupDockerSql.ps1`), the app, and docker-compose. Leave it unset and everything defaults to `TiminuteAdmin.` (as shipped in `appsettings.json`). Set it to use your own password; if you change it after the container already exists, re-run `SetupDockerSql.ps1` to recreate the container on the new password.

Seeded test users (passwords are intentionally trivial — local dev only):

| Email | Role |
|---|---|
| test1@email.com | Basic |
| test2@email.com | Basic |
| test3@email.com | Basic |

## Run with Docker

```bash
git clone https://github.com/jame581/Timinute.git
cd Timinute
cp .env.example .env
# edit .env: set MSSQL_SA_PASSWORD and IdentityServer__Authority
docker compose up -d
```

The app comes up on `http://localhost:8080`. For real deployments, put a TLS-terminating reverse proxy in front and set `IdentityServer__Authority` to the public https URL. Full self-host guide (reverse proxy, external SQL, backups, upgrades): [`docs/DOCKER.md`](docs/DOCKER.md).

## AI / MCP

Timinute hosts a [Model Context Protocol](https://modelcontextprotocol.io/) server at `/mcp`, so an AI assistant like Claude Code or Claude Desktop can query and (optionally) log your time using a personal access token you create at `/settings/tokens`. Tokens are scoped `read` or `read_write`, shown once at creation, and every tool call is recorded to an AI activity log at `/settings/ai-activity`. See [`docs/MCP.md`](docs/MCP.md) for the connection guide and security notes.

## Production deployment

The defaults in `appsettings.json` are tuned for local development on `https://localhost:7047`. For any non-local deployment you'll want to override at least these three:

**1. IdentityServer authority** — JWT issuer + OIDC discovery endpoint. If left at the localhost default, tokens issued by your deployed instance will be rejected at validation. Override via env var:

```bash
IdentityServer__Authority=https://timinute.example.com
```

…or in `appsettings.Production.json`:

```json
{
  "IdentityServer": { "Authority": "https://timinute.example.com" }
}
```

**2. Connection string** — `appsettings.json` ships with the local Docker SA password so `dotnet run` works out of the box. Override for production:

```bash
ConnectionStrings__DefaultConnection="Server=...;Database=Timinute;User Id=...;Password=...;TrustServerCertificate=True;Encrypt=True"
```

**3. Persistent `/keys` directory** — Duende IdentityServer uses automatic key management in production and writes rotating signing keys to `/keys`. On ephemeral hosts (Docker without a volume mount, App Service slot swaps, scaled-out replicas) this directory disappears or differs per instance, which invalidates JWTs after restart and breaks load balancing. Mount a persistent volume at the container's `/keys` (or override the path via `IdentityServer:KeyManagement:KeyPath` if your hosting prefers a different location).

For Docker:

```bash
docker run -v timinute-keys:/keys ...
```

> **v2.0 migration note:** the migration from IdentityServer4 to Duende dropped the IS4-era `DeviceCodes` / `Keys` / `PersistedGrants` tables. If you're upgrading a database that contained any IS4 grant data, that data is lost — log all users out and have them re-authenticate post-deploy.

> **v2.1 migration note:** `AddUserPreferences` adds three columns to `AspNetUsers` (`Preferences_Theme nvarchar(8)`, `Preferences_WeeklyGoalHours decimal(4,1)`, `Preferences_WorkdayHoursPerDay decimal(4,1)`) with sensible defaults applied to existing rows in one statement. No data loss, no separate UPDATE pass.

## Project layout

```
Timinute/
  Server/         ASP.NET Core Web API + Identity + IdentityServer
  Client/         Blazor WebAssembly SPA (Aurora design system)
  Shared/         DTOs shared between client and server
  Server.Tests/   xUnit + Moq; EF InMemory + SQLite test providers
  Client.Tests/   bUnit + xUnit + Moq
docs/superpowers/
  specs/          Per-feature design specs
  plans/          Active plans
  plans/done/     Shipped plans, kept for history
```

## Roadmap

See [`docs/superpowers/plans/feature-roadmap.md`](docs/superpowers/plans/feature-roadmap.md) for the current feature set, P1/P2 backlog, and tech-debt list. Active design specs live alongside in `docs/superpowers/specs/`.

## Author

**Jan Mesarč** — *Creator* — [jame581](https://github.com/jame581)

If Timinute is useful to you, [Buy Me A Coffee](https://www.buymeacoffee.com/jame581) ☕.

## License

MIT — see [LICENSE](LICENSE).
