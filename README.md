# Timinute

[![Build & Test](https://github.com/jame581/Timinute/actions/workflows/build_test.yml/badge.svg)](https://github.com/jame581/Timinute/actions/workflows/build_test.yml)

The free, open-source time tracker that respects your minutes. Track work hours across projects, see exactly where your time goes, and get clear weekly overviews — all in a self-hostable Blazor WebAssembly app.

Originally a demo of modern Blazor; now fully redesigned around the **Aurora** visual system (v2.0) with a tokenized design system, hand-built SVG charts, a custom calendar, and full mobile-responsive treatment.

## Highlights

- **Time tracker** with a real-time stopwatch, session-storage persistence across reload, and undo on delete.
- **Calendar** — desktop week view with a current-time line, mobile day view with a 7-day strip selector. Click an empty cell to add, click an event to edit.
- **Projects** with user-picked colors, monthly stats, and per-project sparklines.
- **Dashboard** — gradient hero stat card, top-project + last-month tiles, hand-built SVG bar chart and donut, recent activity list.
- **Trash** — 30-day soft-delete recovery for projects and tasks, cascade-restore on Project, background hard-purge service.
- **Search + filter + export** — date range, project, name, task-count filters; CSV and Excel exports.
- **Identity** — Duende IdentityServer for auth (JWT for the API, cookie for Identity UI), Basic/Admin roles, lockout, registration via Razor Pages.
- **Mobile responsive** — bottom glass tab bar with FAB, slide-up overflow sheet, full reflow at ≤768px.
- **Accessibility** — `prefers-reduced-motion` honored, `aria-current` on active nav, focus-visible outlines, modal sheet semantics.

## Tech stack

.NET 10 · Blazor WebAssembly (hosted) · EF Core 10 · SQL Server · Duende IdentityServer · Radzen.Blazor (dialogs/notifications only — design system is custom Aurora) · xUnit + Moq + EF InMemory.

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

Seeded test users (passwords are intentionally trivial — local dev only):

| Email | Role |
|---|---|
| test1@email.com | Basic |
| test2@email.com | Basic |
| test3@email.com | Basic |

## Project layout

```
Timinute/
  Server/         ASP.NET Core Web API + Identity + IdentityServer
  Client/         Blazor WebAssembly SPA (Aurora design system)
  Shared/         DTOs shared between client and server
  Server.Tests/   xUnit + Moq + EF InMemory
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
