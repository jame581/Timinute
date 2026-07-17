# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository.

**This file is checked into git and shared with the team.** Keep machine-specific or personal notes in `.claude.local.md` (gitignored) instead.

## What this is

Timinute — a self-hostable time tracker built as a hosted Blazor WebAssembly app on .NET 10. The Server project hosts the WASM Client and exposes the API; there is one deployable unit.

## Branches & releases

- `develop` — working branch; PRs target this. Pushes publish a `develop`-tagged Docker image to ghcr.io.
- `master` — stable/release branch. Anything committed directly to `master` must be back-merged into `develop` immediately, or the next release PR conflicts. Watch out: `gh pr merge` can leave your local HEAD on `master` — check `git branch --show-current` before your next commit.
- Tags `v*` trigger the release workflow (platform packages) and `latest`/semver Docker images.
- CI (`build_test.yml`) runs restore/build/test on pushes and PRs to both branches.

## Commands

```powershell
# Build & test (what CI runs)
dotnet build Timinute.sln --configuration Release
dotnet test Timinute.sln

# Single test / single class
dotnet test Timinute/Server.Tests/Timinute.Server.Tests.csproj --filter "FullyQualifiedName~ProjectControllerTest"

# Run the app (Server hosts the WASM client; https://localhost:7047, Swagger at /swagger in Development)
dotnet run --project Timinute/Server/Timinute.Server.csproj

# Local infra (from repo root)
.\scripts\SetupDockerSql.ps1     # SQL Server 2025 container on port 44555
.\scripts\MigrateDatabase.ps1    # apply EF Core migrations

# New migration (run from scripts/ — paths are relative to it)
.\scripts\AddMigration.ps1 -name MigrationName
```

Seeded dev users: `test1@email.com` / `test2@email.com` / `test3@email.com` (Basic role; trivial passwords, local only).

## Solution layout

```
Timinute/
  Server/         ASP.NET Core API + ASP.NET Identity + Duende IdentityServer; hosts the Client
  Client/         Blazor WASM SPA (Aurora design system)
  Shared/         DTOs + custom validation attributes shared by both
  Server.Tests/   xUnit + Moq; EF InMemory + SQLite helpers
  Client.Tests/   bUnit + xUnit + Moq
docs/superpowers/
  specs/          Per-feature design specs (written before implementation)
  plans/          feature-roadmap.md + active plans; plans/done/ for shipped ones
docs/DOCKER.md    Self-host guide
```

## Architecture

**Auth is dual-scheme** (`Server/Program.cs`): a policy scheme routes requests with a `Bearer` header to JWT validation (audience `Timinute.ServerAPI`) and everything else to the Identity cookie scheme. Duende IdentityServer config (client, scopes, resources) is in-memory in `Program.cs`; the Client authenticates via OIDC code + PKCE. Identity UI (login/register) is server-side Razor Pages under `Server/Areas/Identity`, skinned with `aurora-identity.css`. Roles: Basic, Admin.

**Data access** goes through a generic repository + factory (`Server/Repository/`): controllers take `IRepositoryFactory` and create `IRepository<TEntity>`. The repository provides paging (`GetPaged` with dynamic-LINQ `orderBy` strings), string-based `includeProperties`, and soft delete (`SoftDelete`/`Restore`/`GetDeleted`/`PurgeExpired`). Soft delete is enforced by an EF **global query filter** — read the `CountAll` vs `CountAsync` and `SumAsync` doc comments in `IRepository.cs` before adding aggregate queries; some aggregates deliberately materialize client-side because they don't translate to SQL.

**Ownership checks on every controller action** — all domain data is user-scoped; controllers filter by the authenticated user's id. Preserve this on any new endpoint. Client-supplied foreign keys need the same treatment: verify the FK belongs to the caller, normalize whitespace to `null`, and `Trim()` (SQL Server's trailing-space padding otherwise lets `"Id "` through the check and persists it untrimmed). Inbound AutoMapper maps must `.Ignore()` nested navigation DTOs (e.g. `CreateTrackedTaskDto.Project`) or a client can attach a whole entity to the insert graph.

**Analytics** (`Server/Controllers/AnalyticsController.cs`): four range endpoints (`summary`/`daily`/`projects`/`tags`) filter by user + date range in SQL, then group and sum **in memory** — `SUM` over `TimeSpan` does not translate to SQL. `TzOffsetMinutes` buckets days by the user's local calendar day. On the client, `AnalyticsService` is a **singleton** URL-keyed cache, and must stay one: `AnalyticsCacheInvalidationHandler` (a `DelegatingHandler` that clears the cache on any successful non-GET) is constructed in HttpClientFactory's own DI scope, so a scoped service would clear the wrong instance. Cache keys truncate range ends to the minute — a tick-precision `Now` makes the cache inert.

**Other server pieces:** AutoMapper profile in `Server/MappingProfile.cs`; `TrashPurgeService` is a hosted background service hard-purging soft-deleted rows after 30 days; `ExportService` (ClosedXML) backs CSV/Excel export; model-validation errors return 422 `application/problem+json`. API versioning is `Asp.Versioning.Mvc` with implicit v1.0 — **`AddApiVersioning()` is silently inert on MVC controllers unless `.AddMvc()` is chained onto it** (options register, tests pass, nothing is enforced). Request logging is the built-in `AddHttpLogging`, off by default behind `HttpLogging__Enabled`. All date columns and DTOs use `DateTimeOffset` (UTC-normalized).

**Client:** pages in `Client/Pages`, singleton-style UI services in `Client/Services` (theme, viewport, mobile sheet, undo notifications, project colors, user profile). Radzen.Blazor is used **only** for dialogs/notifications — the visual system is the custom Aurora token set in `wwwroot/css/aurora.css` (dark mode via `[data-theme="dark"]`, pre-Blazor `theme-bootstrap.js` prevents flash). Don't introduce Radzen visual components or Bootstrap styling for new UI; use Aurora tokens. Two rules that are easy to miss: `aurora.css` declares `color-scheme` on `:root` / `[data-theme="dark"]` — keep it, or browser-drawn widgets (`<select>` option popups, date pickers, scrollbars) render light-on-light in dark mode, which screenshots cannot reveal because they're OS-level windows. And since all timestamps are UTC, client formatters must `.ToLocalTime()` before display.

**Shared:** DTOs live in `Shared/Dtos`, validated with Data Annotations plus custom attributes in `Shared/Validators` (`MinDurationAttribute`, `NonDefaultDateTimeOffsetAttribute`).

## Testing notes

- `Server.Tests/Helpers/TestHelper.cs` builds contexts two ways: `GetDefaultApplicationDbContext` (EF InMemory, seeded fixture) and `GetSqliteApplicationDbContext` (in-memory SQLite over a caller-owned open connection). **InMemory silently client-evaluates queries that fail SQL translation and ignores unique constraints — use the SQLite helper for any test that must prove a query translates** (aggregates, paging over includes). SQLite relies on the model's `HasData` seed via `EnsureCreatedAsync`, not `FillInitData`.
- Controller tests inherit `ControllerTestBase`; client component tests use bUnit with `StubHttpMessageHandler`.
- `Server.Tests/Integration/` drives the real pipeline through `WebApplicationFactory<Program>` (`TiminuteApiFactory` + a test auth scheme) — that's how the `[ApiController]` 422 short-circuit is covered. Swapping the DB provider there needs `RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>))` **in addition to** `DbContextOptions<>`, or startup dies with "multiple database providers registered".
- The SQLite provider can't translate `DateTimeOffset` range comparisons, so `OnModelCreating` applies a provider-guarded `DateTimeOffsetToBinaryConverter`. Its binary ordering is only correct because every persisted date is UTC — seed SQLite-backed tests with `TimeSpan.Zero` offsets only.

## Claude Code automation in this repo

- **Hooks** (`.claude/settings.json` + `.claude/hooks/*.ps1`): edits to `.env`, `Timinute/Server/keys/`, or `tempkey.jwk` are blocked; every `.cs` edit is whitespace-formatted via `dotnet format` against `.editorconfig`.
- **Skills**: `/add-migration <Name>` runs the full EF migration flow (generate → verify snapshot diff → apply → test); the `verify` skill covers launching the app and driving changes end-to-end.
- **Subagents**: run `ef-repository-reviewer` after changing controllers/repositories/EF queries, and `auth-config-reviewer` after touching Program.cs auth, Identity, cookies, forwarded headers, or key management — both before committing.

## Workflow conventions

- New features get a design spec in `docs/superpowers/specs/` before implementation; the roadmap (`docs/superpowers/plans/feature-roadmap.md`) tracks scope, backlog, and tech debt.
- Schema changes: add a migration via `scripts/AddMigration.ps1` (output goes to `Server/Data/Migrations`). Startup can auto-migrate when `DatabaseMigrationOnStartup=true` (Docker sets this).
- Production config is env-var driven: `IdentityServer__Authority`, `ConnectionStrings__DefaultConnection`, and a persistent `/keys` volume for IdentityServer signing keys — see README "Production deployment" before touching auth/key config.
