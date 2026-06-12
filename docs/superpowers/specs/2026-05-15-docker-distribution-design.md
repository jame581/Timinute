# Docker Distribution — Design

_Spec date: 2026-05-15._

## Purpose

Add Docker as a second, equal distribution channel for Timinute alongside the existing per-OS release bundles. Goal: a self-hoster can stand up Timinute with three commands.

The existing GitHub Releases flow (linux-x64 + win-x64 tarballs from `release.yml`) is unchanged. This work is purely additive.

## Decisions

| Axis | Choice |
|------|--------|
| Scope | Image **+** repo-shipped `docker-compose.yml` (app + SQL Server bundle) |
| DB migrations | Auto-apply on container startup, gated by `DatabaseMigrationOnStartup` (default `true`) |
| Base image | `mcr.microsoft.com/dotnet/aspnet:10.0` (Debian) |
| Architectures | `linux/amd64` + `linux/arm64` |
| Registry | GHCR — `ghcr.io/jame581/timinute` |
| Image tag format | Unprefixed semver (Docker convention) — `:latest`, `:2.1.0`, `:2.1`, `:2`, `:develop` |
| Publish trigger | `v*` tag → `:latest` + `:X.Y.Z` + `:X.Y` + `:X`; `develop` push → `:develop` |
| HTTPS | HTTP-only in container; user terminates TLS upstream; `ForwardedHeaders` middleware explicit in code |
| Secrets | `.env.example` checked in; user copies to gitignored `.env` |

## Architecture overview

### New files

| Path | Purpose |
|------|---------|
| `Dockerfile` (repo root) | Multi-stage build: SDK restore/publish → aspnet runtime. Non-root `app` user. Exposes 8080. Cross-arch via `$BUILDPLATFORM` + `$TARGETARCH`. |
| `.dockerignore` | Strips `bin/`, `obj/`, `.vs/`, `.git/`, `docs/`, `screenshots/`, `Server.Tests/`, `*.md`, etc. from build context. |
| `docker-compose.yml` (repo root) | Two services (`app`, `db`), two named volumes (`timinute-keys`, `timinute-data`), env from `.env`. |
| `.env.example` | Placeholders for `MSSQL_SA_PASSWORD`, `IdentityServer__Authority`, `TIMINUTE_PORT`, `TIMINUTE_TAG`. |
| `.github/workflows/docker-publish.yml` | Multi-arch build + push to GHCR on `v*` tag and `develop` push. Also `workflow_dispatch` for manual CI test. |
| `docs/DOCKER.md` | Full self-host guide. |

### Modified files

| Path | Change |
|------|--------|
| `Timinute/Server/Program.cs` | (1) Configure `ForwardedHeadersOptions` and call `app.UseForwardedHeaders()` early in the pipeline. (2) Add startup migration gated by `DatabaseMigrationOnStartup` config (default `true`). |
| `README.md` | New "Run with Docker" subsection above "Production deployment"; GHCR badge in the badge block. |
| `docs/superpowers/plans/feature-roadmap.md` | After merge: add a "Docker distribution" row to "Recently shipped." |
| `.gitignore` | Add `.env`. |

### Not touched

- `release.yml` — unchanged. Docker pipeline is a separate workflow.
- `build_test.yml` — unchanged.
- `scripts/SetupDockerSql.ps1`, `scripts/MigrateDatabase.ps1` — kept; they serve the dev-workflow audience, not the self-host audience.

## File contents

### `Dockerfile`

```dockerfile
# syntax=docker/dockerfile:1.7

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# csproj-only first → restore layer caches well
COPY Timinute.sln ./
COPY Timinute/Server/Timinute.Server.csproj  Timinute/Server/
COPY Timinute/Client/Timinute.Client.csproj  Timinute/Client/
COPY Timinute/Shared/Timinute.Shared.csproj  Timinute/Shared/
RUN dotnet restore Timinute/Server/Timinute.Server.csproj -a $TARGETARCH

COPY . .
RUN dotnet publish Timinute/Server/Timinute.Server.csproj \
        -c Release -a $TARGETARCH --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
USER app
COPY --from=build --chown=app:app /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    DatabaseMigrationOnStartup=true
EXPOSE 8080
VOLUME ["/keys"]
ENTRYPOINT ["dotnet", "Timinute.Server.dll"]
```

`$BUILDPLATFORM` + `$TARGETARCH` keeps the SDK stage running native on the buildx host; only the published binaries are cross-targeted. Avoids QEMU-emulated SDK execution which is ~5× slower.

### `docker-compose.yml`

```yaml
name: timinute

services:
  app:
    image: ghcr.io/jame581/timinute:${TIMINUTE_TAG:-latest}
    restart: unless-stopped
    depends_on:
      db:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnection: >-
        Server=db,1433;Database=Timinute;User Id=sa;
        Password=${MSSQL_SA_PASSWORD:?set MSSQL_SA_PASSWORD in .env};
        TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=true
      IdentityServer__Authority: ${IdentityServer__Authority:?set IdentityServer__Authority in .env}
    ports:
      - "${TIMINUTE_PORT:-8080}:8080"
    volumes:
      - timinute-keys:/keys

  db:
    image: mcr.microsoft.com/mssql/server:2025-latest
    restart: unless-stopped
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: ${MSSQL_SA_PASSWORD:?set MSSQL_SA_PASSWORD in .env}
      MSSQL_PID: Express
    volumes:
      - timinute-data:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -No -Q 'SELECT 1' || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 30
      start_period: 30s

volumes:
  timinute-keys:
  timinute-data:
```

Notes:
- `${VAR:?...}` makes compose fail with a clear message if the user skipped `.env` setup.
- The `sqlcmd` path `/opt/mssql-tools18/bin/sqlcmd` matches SQL Server 2022+ images; if 2025 image relocates it, swap to a TCP probe (`bash -c "</dev/tcp/localhost/1433"`).
- External-SQL users comment out the `db` service and edit the `ConnectionStrings__DefaultConnection` line. Documented in `DOCKER.md`.

### `.env.example`

```bash
# Copy to .env and edit. .env is gitignored.

# Bundled SQL Server SA password. SQL Server requires 8+ chars,
# mix of upper/lower/digit/symbol.
MSSQL_SA_PASSWORD=ChangeMe.Strong-Password-123!

# Public URL Timinute is reachable on. MUST be https in production
# and MUST exactly match the URL your reverse proxy serves (no trailing slash).
IdentityServer__Authority=https://timinute.example.com

# Host port. Container always listens on 8080 internally.
TIMINUTE_PORT=8080

# Image tag: `latest` (release), `develop` (preview), or a pinned version.
TIMINUTE_TAG=latest
```

### `.dockerignore`

```
**/bin
**/obj
**/.vs
**/.git
**/node_modules
docs
screenshots
Server.Tests
*.md
.github
.env
.env.*
!.env.example
```

### `Program.cs` changes

**ForwardedHeaders.** Current `ASPNETCORE_FORWARDEDHEADERS_ENABLED` env-var auto-enables the middleware with defaults that only trust loopback. Inside a Docker network, the reverse proxy is not loopback, so X-Forwarded-* headers would be dropped and OIDC issuer validation would fail. Configure explicitly:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                       | ForwardedHeaders.XForwardedProto
                       | ForwardedHeaders.XForwardedHost;
    o.KnownNetworks.Clear();   // accept from any network
    o.KnownProxies.Clear();    // acceptable inside a private Docker network
});

// In pipeline — BEFORE UseAuthentication / IdentityServer:
app.UseForwardedHeaders();
```

**Auto-migrate at startup.** Configurable gate so multi-replica or DBA-managed setups can disable:

```csharp
if (app.Configuration.GetValue("DatabaseMigrationOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
         .Database.Migrate();
}
```

Auto-migrate is config-gated (`DatabaseMigrationOnStartup`, default `true`). `UseForwardedHeaders` is unconditional but is a no-op when no `X-Forwarded-*` headers reach the app — which is exactly the `dotnet run` localhost case. So the existing dev workflow is unaffected at the network layer; the only behavior shift for dev users is that `dotnet run` will now also apply pending EF migrations on boot (was previously a manual `MigrateDatabase.ps1` step). Migrations are idempotent and the script remains for any user who wants to opt out via `DatabaseMigrationOnStartup=false`.

### `.github/workflows/docker-publish.yml`

```yaml
name: Docker Publish

on:
  push:
    tags: ['v*']
    branches: [develop]
  workflow_dispatch:

permissions:
  contents: read
  packages: write

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: docker/setup-qemu-action@v3
        with: { platforms: arm64 }
      - uses: docker/setup-buildx-action@v3
      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - id: meta
        uses: docker/metadata-action@v5
        with:
          images: ghcr.io/jame581/timinute
          tags: |
            type=raw,value=latest,enable=${{ startsWith(github.ref, 'refs/tags/v') }}
            type=semver,pattern={{version}}
            type=semver,pattern={{major}}.{{minor}}
            type=semver,pattern={{major}}
            type=ref,event=branch,enable=${{ github.ref == 'refs/heads/develop' }}

      - uses: docker/build-push-action@v6
        with:
          context: .
          platforms: linux/amd64,linux/arm64
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

`type=semver` strips the `v` prefix by default, so git tag `v2.2.0` → image `:2.2.0` / `:2.2` / `:2` / `:latest`. Docker convention; matches `mcr.microsoft.com/mssql/server:2025-latest` and most upstream images.

### `docs/DOCKER.md` — outline

1. **Quick start** — clone, copy `.env.example`, edit, `docker compose up -d`.
2. **Configuration reference** — every env var the app reads, with defaults.
3. **Volumes & data** — what each volume holds, backup/restore commands.
4. **Reverse proxy examples** — Caddyfile snippet (most ergonomic for self-host), nginx snippet, Traefik labels snippet.
5. **External SQL Server** — comment out the `db` service, override connection string.
6. **Upgrading** — `docker compose pull && docker compose up -d`. Forward-only; back-up DB first; migrations bake into the image.
7. **Disabling auto-migrate** — `DatabaseMigrationOnStartup=false` for multi-replica / DBA-managed setups.
8. **Troubleshooting** — common errors: OIDC issuer mismatch, lost `/keys` invalidating JWTs, SQL slow first boot, X-Forwarded-Proto misconfig.

### `README.md` change

Insert above "Production deployment":

```markdown
## Run with Docker

[![GHCR](https://img.shields.io/badge/ghcr.io-jame581%2Ftiminute-blue?logo=docker)](https://github.com/jame581/Timinute/pkgs/container/timinute)

\`\`\`bash
git clone https://github.com/jame581/Timinute.git
cd Timinute
cp .env.example .env
# edit .env: set MSSQL_SA_PASSWORD and IdentityServer__Authority
docker compose up -d
\`\`\`

Full self-host guide (reverse proxy, external SQL, backups, upgrades): [`docs/DOCKER.md`](docs/DOCKER.md).
```

Plus add the GHCR badge to the top-of-file badge block.

## Versioning & upgrade rules

- Releases (`v*` tag push) → `:latest`, `:X.Y.Z`, `:X.Y`, `:X` all advance.
- `develop` push → `:develop` advances. Preview channel, at-your-own-risk.
- Schema is forward-only. Image baked with migrations N can't be downgraded once DB has migrated. Documented under "Upgrading."
- For deterministic deploys, pin by digest (`ghcr.io/jame581/timinute@sha256:...`). Documented.

## Testing strategy

No new xUnit tests. Both code edits (ForwardedHeaders config, conditional `Database.Migrate()`) are 5-line gates around framework-tested code; unit tests would test the framework, not Timinute.

`workflow_dispatch` on the new workflow allows manual CI test before cutting a real release tag.

**Manual verification checklist (must pass before merge):**

1. `docker compose up -d` on a clean host → app reachable at `http://localhost:8080`; register a user; track a task; see it persisted.
2. `docker compose down && docker compose up -d` → no data loss.
3. `docker compose down -v && docker compose up -d` → fresh DB; migrations apply cleanly; app starts without errors.
4. Behind Caddy with a real https cert → OIDC login works end-to-end (`X-Forwarded-Proto` honored; issuer matches `IdentityServer__Authority`).
5. Restart container → `/keys` survives → JWT signing keys stable; users stay logged in.
6. Pull image on `linux/arm64` host (Apple Silicon or Pi 5) → starts and serves correctly.
7. Upgrade path: pull `:2.1.0`, start; then pull `:2.2.0` (or `:develop`); `docker compose up -d` → migrations apply on startup; no manual intervention.

## Out of scope (v1)

- Hadolint Dockerfile lint job in `build_test.yml`
- Cosign image signing / SBOM attestation
- Separate migration-runner container (single-service-per-pod pattern)
- Helm chart / Kubernetes manifests
- Docker Hub mirror
- `PathBase` / subpath hosting
- SQLite / Postgres provider support
- `linux/arm/v7` (Pi 3 / older — SQL Server doesn't run there anyway)
- Channel-pinning UX beyond what semver tags give us

Any of the above can be a follow-up spec if user demand surfaces.

## Risks & open questions

- **`sqlcmd` path in SQL Server 2025 image** — assumed `/opt/mssql-tools18/bin/sqlcmd` per 2022+ convention. If the 2025 image moves it, fallback is a TCP-port probe in the healthcheck. Verified during implementation.
- **`mcr.microsoft.com/dotnet/aspnet:10.0` non-root `app` user** — assumed pre-created (true since .NET 8). Verified during implementation; trivial to add `RUN useradd app` if not.
- **Existing dev workflow regression risk** — `UseForwardedHeaders` is unconditional but a no-op without `X-Forwarded-*` headers, so localhost dev is unchanged at the network layer. Default `DatabaseMigrationOnStartup=true` means `dotnet run` will now also apply pending EF migrations on boot — a behavior shift for dev users who relied on `MigrateDatabase.ps1` manually. Acceptable: the script remains, the migration call is idempotent, and the gate flips off via `DatabaseMigrationOnStartup=false`. Call it out in the PR notes so dev users aren't surprised.
- **Forwarded-headers cleared `KnownProxies` security** — accepting `X-Forwarded-*` from any source is fine inside a closed Docker network but would be a header-spoofing risk if the container were directly internet-exposed. Documented in `DOCKER.md`: "must be behind a reverse proxy in production."
