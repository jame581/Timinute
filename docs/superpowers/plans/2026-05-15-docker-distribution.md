# Docker Distribution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Docker image + bundled `docker-compose.yml` as a second distribution channel for Timinute, published to GHCR as `ghcr.io/jame581/timinute` for `linux/amd64` + `linux/arm64`, while leaving the existing per-OS release tarballs untouched.

**Architecture:** Multi-stage `Dockerfile` on `mcr.microsoft.com/dotnet/aspnet:10.0` (Debian). Bundled compose pairs the app with `mcr.microsoft.com/mssql/server:2025-latest` and named volumes for `/keys` (IdentityServer signing keys) and the SQL data dir. Two small Program.cs edits — explicit `ForwardedHeaders` middleware (for reverse-proxy compatibility) and a config-gated `Database.Migrate()` at startup — round out the runtime. Publishing happens via a new `docker-publish.yml` workflow triggered on `v*` tags (release channel) and `develop` pushes (preview channel).

**Tech Stack:** Docker, docker compose v2, GitHub Actions, `docker/build-push-action`, `docker/metadata-action`, ASP.NET Core 10, EF Core 10.

**Source spec:** `docs/superpowers/specs/2026-05-15-docker-distribution-design.md` (commit `1dff0d7`).

**Working branch:** All tasks run on a new feature branch `feature/docker-distribution`, PR'd into `develop`. Merging to `develop` publishes the `:develop` image automatically. A later `v*` tag from `master` publishes `:latest`/`:X.Y.Z`/`:X.Y`/`:X`.

---

## Task 0: Create the feature branch

**Files:** none

- [ ] **Step 1: Confirm clean working tree on `develop`**

Run: `git status && git rev-parse --abbrev-ref HEAD`
Expected: `nothing to commit, working tree clean` and current branch `develop`.

- [ ] **Step 2: Create and switch to the feature branch**

Run: `git checkout -b feature/docker-distribution`
Expected: `Switched to a new branch 'feature/docker-distribution'`

---

## Task 1: Gitignore `.env`

**Files:**
- Modify: `.gitignore` (append at end)

- [ ] **Step 1: Append `.env` rule**

Append these lines to the end of `.gitignore`:

```gitignore

# Docker self-host runtime config (copy of .env.example)
.env
```

(Leading blank line keeps the section separated from the prior block.)

- [ ] **Step 2: Verify it doesn't ignore `.env.example`**

Run: `git check-ignore -v .env.example`
Expected: exit code `1` and no output (file is NOT ignored). If it shows a match, the rule is wrong — `.env` (no leading dot-glob) shouldn't catch `.env.example`.

- [ ] **Step 3: Commit**

```bash
git add .gitignore
git commit -m "chore(docker): gitignore .env runtime config"
```

---

## Task 2: Create `.dockerignore`

**Files:**
- Create: `.dockerignore` (repo root)

- [ ] **Step 1: Write the file**

Create `.dockerignore` with:

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

- [ ] **Step 2: Commit**

```bash
git add .dockerignore
git commit -m "chore(docker): add .dockerignore"
```

---

## Task 3: Create `.env.example`

**Files:**
- Create: `.env.example` (repo root)

- [ ] **Step 1: Write the file**

Create `.env.example` with:

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

# Image tag: `latest` (release), `develop` (preview), or a pinned version like `2.1.0`.
TIMINUTE_TAG=latest
```

- [ ] **Step 2: Commit**

```bash
git add .env.example
git commit -m "chore(docker): add .env.example template"
```

---

## Task 4: Add `ForwardedHeaders` middleware to `Program.cs`

**Files:**
- Modify: `Timinute/Server/Program.cs` (add using, add Configure call before `builder.Build()`, add `UseForwardedHeaders` before `UseHttpsRedirection`)

**Why:** The default ASP.NET `ASPNETCORE_FORWARDEDHEADERS_ENABLED` env var auto-enables the middleware with `KnownProxies` limited to loopback. Inside a Docker network, the reverse proxy isn't loopback, so `X-Forwarded-Proto` would be dropped and `IdentityServer` would see `http://` instead of the issuer's `https://`. Configure explicitly so that `KnownNetworks` and `KnownProxies` are cleared — accepting forwarded headers from any source, which is safe inside a private Docker network behind a single reverse proxy.

- [ ] **Step 1: Add the namespace import**

At the top of `Timinute/Server/Program.cs`, after the existing `using Microsoft.AspNetCore.*` block (around line 4), add:

```csharp
using Microsoft.AspNetCore.HttpOverrides;
```

- [ ] **Step 2: Register the options before `builder.Build()`**

In `Timinute/Server/Program.cs`, immediately before `var app = builder.Build();` (around line 104), insert:

```csharp
// Reverse-proxy support. Docker users terminate TLS upstream; we trust
// X-Forwarded-* from any source inside the private container network.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto
                             | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

```

- [ ] **Step 3: Wire the middleware into the pipeline**

In the same file, immediately before `app.UseHttpsRedirection();` (currently line 128 — line number will shift up by the lines added in Step 2), insert:

```csharp
app.UseForwardedHeaders();
```

The pipeline order must be: `UseForwardedHeaders` → `UseHttpsRedirection` → … → `UseIdentityServer` → `UseAuthentication`. Adding it before `UseHttpsRedirection` satisfies that.

- [ ] **Step 4: Build and verify no compile errors**

Run: `dotnet build Timinute.sln --configuration Release`
Expected: `Build succeeded`. Zero errors. Warnings are fine.

- [ ] **Step 5: Run existing tests, confirm no regressions**

Run: `dotnet test Timinute.sln --configuration Release --no-build`
Expected: all existing tests pass.

- [ ] **Step 6: Smoke-test locally**

Run: `dotnet run --project Timinute/Server/Timinute.Server.csproj`
Expected: app starts, no startup exceptions, `https://localhost:7047` serves the landing page. Stop with `Ctrl+C`.

(This validates the middleware insertion didn't break the dev-mode pipeline. Full reverse-proxy behavior is verified later in the compose smoke test.)

- [ ] **Step 7: Commit**

```bash
git add Timinute/Server/Program.cs
git commit -m "feat(server): explicit ForwardedHeaders for reverse-proxy compatibility"
```

---

## Task 5: Add config-gated auto-migrate to `Program.cs`

**Files:**
- Modify: `Timinute/Server/Program.cs` (insert after `var app = builder.Build();`)

**Why:** Docker users need the app to apply pending EF migrations on container start — there's no path to run `dotnet ef` against the containerized DB from outside. Gate behind a config flag (`DatabaseMigrationOnStartup`, default `true`) so multi-replica or DBA-managed setups can disable it. Dev users on `dotnet run` keep their existing flow but get the same idempotent migration call for free — `MigrateDatabase.ps1` continues to work for the "I haven't yet run the app" case.

- [ ] **Step 1: Insert the migration block**

In `Timinute/Server/Program.cs`, immediately after `var app = builder.Build();` (around line 104 from the previous task — line number will have shifted), insert:

```csharp

if (app.Configuration.GetValue("DatabaseMigrationOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
         .Database.Migrate();
}

```

`Timinute.Server.Data` (which exports `ApplicationDbContext`) is already imported at the top of the file (line 11). `Microsoft.EntityFrameworkCore` is already imported (line 5).

- [ ] **Step 2: Build and verify no compile errors**

Run: `dotnet build Timinute.sln --configuration Release`
Expected: `Build succeeded`.

- [ ] **Step 3: Run existing tests, confirm no regressions**

Run: `dotnet test Timinute.sln --configuration Release --no-build`
Expected: all existing tests pass.

- [ ] **Step 4: Smoke-test locally (idempotent migrate)**

Run: `dotnet run --project Timinute/Server/Timinute.Server.csproj`
Expected: app starts. Check console output: should see EF Core log lines mentioning `Applying migration` only if the dev SQL container has un-applied migrations; otherwise no migration logs. App reachable at `https://localhost:7047`. Stop with `Ctrl+C`.

- [ ] **Step 5: Smoke-test the disable flag**

Run: `dotnet run --project Timinute/Server/Timinute.Server.csproj --no-launch-profile -- --DatabaseMigrationOnStartup=false` (or set `DatabaseMigrationOnStartup=false` in environment, then `dotnet run`).
Expected: app starts. Zero EF Core migration log lines. (If your dev DB was already up-to-date in Step 4 there'd be no migration logs there either, so this is a confirming-no-attempt smoke, not a hard differentiator. Look for the explicit `Migrate` activity to be absent.)

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server/Program.cs
git commit -m "feat(server): auto-apply EF migrations on startup, gated by config"
```

---

## Task 6: Create the `Dockerfile`

**Files:**
- Create: `Dockerfile` (repo root)

- [ ] **Step 1: Write the file**

Create `Dockerfile` at the repo root with:

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

- [ ] **Step 2: Verify Docker is available and buildx is on**

Run: `docker buildx version`
Expected: prints version (e.g., `github.com/docker/buildx v0.14.0 …`). If missing, follow Docker Desktop install or enable the buildx CLI plugin.

- [ ] **Step 3: Native-arch build smoke test**

Run: `docker build -t timinute:dev .`
Expected: builds successfully on the host's native architecture; final image around 280–340 MB. Note the digest.

- [ ] **Step 4: Verify the image runs and dies cleanly without a reachable DB**

Run: `docker run --rm -e ConnectionStrings__DefaultConnection="Server=127.0.0.1,1;Database=x;User Id=sa;Password=ChangeMe.Strong-Password-123!;TrustServerCertificate=True;Encrypt=True" timinute:dev`
Expected: the container starts, attempts to apply migrations against the unreachable DB, throws a SQL-connection exception, and exits with non-zero. This confirms the migration block fires and the app entrypoint is wired correctly. (Real DB connectivity is tested in the compose smoke test in Task 8.)

- [ ] **Step 5: Commit**

```bash
git add Dockerfile
git commit -m "feat(docker): multi-stage Dockerfile on aspnet:10.0"
```

---

## Task 7: Create the `docker-compose.yml`

**Files:**
- Create: `docker-compose.yml` (repo root)

- [ ] **Step 1: Write the file**

Create `docker-compose.yml` at the repo root with:

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

- [ ] **Step 2: Validate the compose file parses**

Run: `docker compose config`
Expected: prints the resolved compose config to stdout (or warns about missing `.env` variables — that's fine, Step 3 fixes it). No "yaml: …" or "schema validation" errors.

- [ ] **Step 3: Commit**

```bash
git add docker-compose.yml
git commit -m "feat(docker): compose bundle with sql server 2025"
```

---

## Task 8: Local end-to-end smoke test

**Files:** none — verification only

This task does not produce a commit. It exercises Tasks 2–7 together to catch any integration issues before adding CI and docs.

- [ ] **Step 1: Set up local `.env`**

```bash
cp .env.example .env
```

Then edit `.env`:
- `MSSQL_SA_PASSWORD=` set to any 8+ char password with mixed case, digit, symbol (e.g. `Local.Test-Pw-123!`).
- `IdentityServer__Authority=` set to `https://localhost:8080`.
- `TIMINUTE_TAG=` change to `dev` (matches the local image tag from Task 6 step 3).
- `TIMINUTE_PORT=` leave at `8080`.

- [ ] **Step 2: Point compose at the local image**

The compose file pulls `ghcr.io/jame581/timinute:${TIMINUTE_TAG}` — but the image hasn't been pushed yet. For this smoke test, override locally by setting `image: timinute:dev` in compose, OR re-tag your local image:

```bash
docker tag timinute:dev ghcr.io/jame581/timinute:dev
```

- [ ] **Step 3: Bring up the stack**

Run: `docker compose up -d`
Expected: `db` container starts and becomes healthy within ~30s; `app` container starts after that. Both report `healthy`/`running` in `docker compose ps`.

- [ ] **Step 4: Watch the app logs for the startup migration**

Run: `docker compose logs app --tail=200`
Expected: EF Core log lines indicating migrations applied (since this is a fresh DB). No exceptions. Final line: `Now listening on: http://[::]:8080`.

- [ ] **Step 5: Hit the app**

Open: `http://localhost:8080` in a browser.
Expected: Timinute landing page renders. (Browser will warn about HTTP on a normally-https origin; ignore for local smoke. Real production needs the reverse-proxy https.)

- [ ] **Step 6: Register a user and track a task**

Manual: register a new account through the UI, log in, create a project, start the stopwatch, stop it, confirm the task appears in the tracked-tasks list.
Expected: full happy path works.

- [ ] **Step 7: Restart-survival test**

Run: `docker compose down && docker compose up -d`
Then log back in with the same credentials. Open the tracked-tasks page.
Expected: user still exists; tracked task still present; user did not have to re-register. Confirms both `timinute-data` (DB) and `timinute-keys` (IdentityServer signing) volumes persist.

- [ ] **Step 8: Fresh-DB test**

Run: `docker compose down -v && docker compose up -d`
Expected: both named volumes are destroyed; on restart, DB is fresh, migrations apply cleanly, app starts without errors. No user/data carryover.

- [ ] **Step 9: Tear down**

Run: `docker compose down -v`
Then revert any compose edits made in Step 2 (the override was only for local testing — the committed compose pulls from ghcr).

- [ ] **Step 10: If `sqlcmd` healthcheck failed**

If the SQL Server 2025 image relocated `sqlcmd`, the `db` healthcheck will time out. Replace the healthcheck `test` block in `docker-compose.yml` with a TCP probe:

```yaml
test: ["CMD-SHELL", "bash -c '</dev/tcp/localhost/1433' || exit 1"]
```

Then commit that fix:

```bash
git add docker-compose.yml
git commit -m "fix(docker): swap sql healthcheck to tcp probe (mssql-tools path moved)"
```

Only do this step if Step 3 failed with `db` stuck in `unhealthy`/`starting`.

---

## Task 9: Create `.github/workflows/docker-publish.yml`

**Files:**
- Create: `.github/workflows/docker-publish.yml`

- [ ] **Step 1: Write the workflow**

Create `.github/workflows/docker-publish.yml` with:

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
        with:
          platforms: arm64

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

- [ ] **Step 2: Commit and push the feature branch**

```bash
git add .github/workflows/docker-publish.yml
git commit -m "ci(docker): publish multi-arch image to ghcr"
git push -u origin feature/docker-distribution
```

- [ ] **Step 3: Trigger the workflow manually from the feature branch**

Run: `gh workflow run docker-publish.yml --ref feature/docker-distribution`
Expected: workflow run starts. Get the run URL:

```bash
gh run list --workflow=docker-publish.yml --branch feature/docker-distribution --limit 1
```

Open the run in browser or `gh run watch <run-id>`.

- [ ] **Step 4: Verify the manual run succeeds**

Expected: all steps green within ~10–15 minutes (arm64 emulation is slow on the first uncached build). The `Extract metadata` step's output should show only ad-hoc tags (no `:latest`, no `:develop`, since this is a `workflow_dispatch` from a non-default branch). The image is pushed to GHCR.

- [ ] **Step 5: Confirm the image is at GHCR**

Open: `https://github.com/jame581/Timinute/pkgs/container/timinute`
Expected: a new manifest with both `linux/amd64` and `linux/arm64` platforms shown.

If the workflow failed: read the failing step's logs, fix the workflow file, recommit, force-push, re-run. Common issues:
- Permissions: PR may need `packages: write` granted on the repo settings for `GITHUB_TOKEN` (Settings → Actions → General → Workflow permissions → "Read and write").
- arm64 timeout: bump `runs-on` to `ubuntu-latest-l` (if available) or accept the slow first build — subsequent builds use `cache-to: type=gha`.

- [ ] **Step 6: (Optional) Pull the pushed image and re-run the Task 8 smoke**

If you want to validate the GHCR-pushed image end-to-end, update local `.env` `TIMINUTE_TAG=<the sha-XXXXXX tag the workflow used>` and re-run `docker compose up -d`. This is belt-and-suspenders; the local image already passed.

---

## Task 10: Create `docs/DOCKER.md`

**Files:**
- Create: `docs/DOCKER.md`

- [ ] **Step 1: Write the file**

Create `docs/DOCKER.md` with:

````markdown
# Running Timinute with Docker

Timinute publishes a multi-arch Docker image (`linux/amd64` + `linux/arm64`) to GitHub Container Registry. The bundled `docker-compose.yml` starts the app alongside a SQL Server container for a 5-minute self-host experience. For production you typically replace the bundled SQL with your own and run Timinute behind a reverse proxy that handles TLS.

## Quick start

```bash
git clone https://github.com/jame581/Timinute.git
cd Timinute
cp .env.example .env
# edit .env: set MSSQL_SA_PASSWORD and IdentityServer__Authority
docker compose up -d
```

The app comes up on `http://localhost:8080` (configurable via `TIMINUTE_PORT`). For production, put a reverse proxy in front with a real TLS cert and set `IdentityServer__Authority` to the public https URL.

## Image tags

| Tag                   | Channel        | When it advances                     |
|-----------------------|----------------|--------------------------------------|
| `latest`              | Stable release | On every `v*` git tag                |
| `2.1.0`, `2.1`, `2`   | Pinned release | On the corresponding `v*` git tag    |
| `develop`             | Preview        | On every push to the `develop` branch |
| `@sha256:…`           | Digest pin     | Never moves — recommended for production |

Pin by digest in production:

```yaml
image: ghcr.io/jame581/timinute@sha256:abc123…
```

## Configuration reference

All settings flow through ASP.NET Core's hierarchical config — env vars with `__` between segments override `appsettings.json`. The most-frequently-overridden ones:

| Variable                                       | Default                          | Purpose                                  |
|------------------------------------------------|----------------------------------|------------------------------------------|
| `ConnectionStrings__DefaultConnection`         | _(set in compose)_               | SQL Server connection string             |
| `IdentityServer__Authority`                    | `https://localhost:7047`         | OIDC issuer — must be your public URL    |
| `DatabaseMigrationOnStartup`                   | `true`                           | Auto-apply EF migrations on container start |
| `ASPNETCORE_ENVIRONMENT`                       | `Production` (in compose)        | Standard ASP.NET env flag                |
| `ASPNETCORE_URLS`                              | `http://+:8080`                  | Listen address inside container          |
| `TrashRetention__Days`                         | `30`                             | Soft-delete retention before hard-purge  |
| `TrashRetention__PurgeIntervalHours`           | `24`                             | How often the purge service runs         |

## Volumes

| Volume          | Mount path inside container | Holds                                      |
|-----------------|-----------------------------|--------------------------------------------|
| `timinute-data` | `/var/opt/mssql`            | SQL Server data + logs (bundled DB only)   |
| `timinute-keys` | `/keys`                     | IdentityServer signing keys — JWT lifetime depends on these surviving restarts |

**Both must persist.** Losing `timinute-data` means losing all user data. Losing `timinute-keys` invalidates every issued JWT and logs every user out.

### Backing up

```bash
# Snapshot SQL data
docker compose exec db bash -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -No -Q 'BACKUP DATABASE Timinute TO DISK = N\"/var/opt/mssql/data/Timinute.bak\" WITH FORMAT, INIT'"
docker cp $(docker compose ps -q db):/var/opt/mssql/data/Timinute.bak ./Timinute-$(date +%Y%m%d).bak

# Snapshot signing keys
docker run --rm -v timinute-keys:/keys -v "$(pwd)":/out alpine tar -czf /out/timinute-keys-$(date +%Y%m%d).tar.gz -C /keys .
```

## Reverse proxy

The container speaks HTTP only. Put a TLS-terminating reverse proxy in front of it. Three examples:

### Caddy (most ergonomic)

```caddyfile
timinute.example.com {
    reverse_proxy timinute-app:8080
}
```

Caddy auto-provisions a Let's Encrypt cert and forwards `X-Forwarded-*` headers correctly out of the box. Add Caddy as a third service in compose, or run it as a separate stack with a shared external Docker network.

### nginx

```nginx
server {
    listen 443 ssl http2;
    server_name timinute.example.com;
    ssl_certificate     /etc/letsencrypt/live/timinute.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/timinute.example.com/privkey.pem;

    location / {
        proxy_pass http://timinute-app:8080;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host  $host;
    }
}
```

### Traefik labels

Add to the `app` service in compose:

```yaml
labels:
  - "traefik.enable=true"
  - "traefik.http.routers.timinute.rule=Host(`timinute.example.com`)"
  - "traefik.http.routers.timinute.entrypoints=websecure"
  - "traefik.http.routers.timinute.tls.certresolver=letsencrypt"
  - "traefik.http.services.timinute.loadbalancer.server.port=8080"
```

## External SQL Server

To use an existing SQL Server instead of the bundled one:

1. Comment out the `db` service block in `docker-compose.yml`.
2. Remove the `depends_on` block on the `app` service.
3. In `.env`, point the connection string at your server:
   ```bash
   # Override the whole connection string. The compose interpolation uses
   # MSSQL_SA_PASSWORD; set it to anything (it won't be used) or refactor compose
   # to read a raw connection string env var directly.
   ```
   For a non-compose-interpolated string, edit the `app.environment.ConnectionStrings__DefaultConnection` line in compose to:
   ```yaml
   ConnectionStrings__DefaultConnection: ${ConnectionStrings__DefaultConnection}
   ```
   then put the full string in `.env`.
4. `MSSQL_SA_PASSWORD` is no longer used; you can remove it from `.env`.

## Upgrading

```bash
docker compose pull
docker compose up -d
```

Migrations apply on startup. Schema is forward-only — once a newer image migrates the DB, you cannot downgrade to an older image. Back up `timinute-data` before upgrading.

## Multi-replica / DBA-managed deployments

Disable startup migration on the app replicas; run a one-shot migration container before rolling the app:

```bash
docker run --rm \
    -e ConnectionStrings__DefaultConnection="…" \
    -e DatabaseMigrationOnStartup=true \
    ghcr.io/jame581/timinute:2.2.0
# ^ exits 0 after migration; app replicas can then start with DatabaseMigrationOnStartup=false
```

## Troubleshooting

**"Invalid issuer" / "Invalid signature" on login**
The issuer URL in `IdentityServer__Authority` must EXACTLY match the URL the user's browser is on — protocol (https), host, no trailing slash, correct port. Mismatch causes JWT validation to fail.

**All users get logged out after `docker compose down/up`**
The `timinute-keys` volume was not persisted. IdentityServer regenerated its signing keys on restart, invalidating every issued token. Confirm the named volume exists: `docker volume ls | grep timinute-keys`.

**SQL container slow on first boot (~30s)**
Normal. SQL Server initializes `master`, `tempdb`, etc. on first start. The healthcheck has a `start_period: 30s` for this. If it takes longer than 60s, check `docker compose logs db` for password-policy errors.

**App can't reach DB**
Check `docker compose logs app` for `Microsoft.Data.SqlClient.SqlException`. Typical causes: `MSSQL_SA_PASSWORD` doesn't satisfy SQL's complexity rules (8+ chars, mixed case + digit + symbol); or you changed the SA password but the `timinute-data` volume has the OLD password baked in (recreate with `docker compose down -v`, beware data loss).

**`X-Forwarded-Proto` not honored — OIDC redirect lands on http://**
Confirm your reverse proxy is actually setting `X-Forwarded-Proto`. nginx requires the explicit `proxy_set_header` line; Caddy and Traefik do it automatically.

````

- [ ] **Step 2: Commit**

```bash
git add docs/DOCKER.md
git commit -m "docs(docker): self-host guide with reverse-proxy + upgrade recipes"
```

---

## Task 11: Update `README.md`

**Files:**
- Modify: `README.md` (top badge block + new "Run with Docker" section before "Production deployment")

- [ ] **Step 1: Add GHCR badge to the top badge block**

In `README.md`, find the existing badge block (lines 3–6):

```markdown
[![Release](https://github.com/jame581/Timinute/actions/workflows/release.yml/badge.svg)](https://github.com/jame581/Timinute/actions/workflows/release.yml)
[![Latest release](https://img.shields.io/github/v/release/jame581/Timinute)](https://github.com/jame581/Timinute/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
```

Append one more badge line below the `.NET` line:

```markdown
[![Docker](https://img.shields.io/badge/ghcr.io-jame581%2Ftiminute-blue?logo=docker)](https://github.com/jame581/Timinute/pkgs/container/timinute)
```

- [ ] **Step 2: Insert "Run with Docker" subsection**

In `README.md`, find the `## Production deployment` heading (around line 87). Immediately before it, insert this section:

````markdown
## Run with Docker

```bash
git clone https://github.com/jame581/Timinute.git
cd Timinute
cp .env.example .env
# edit .env: set MSSQL_SA_PASSWORD and IdentityServer__Authority
docker compose up -d
```

The app comes up on `http://localhost:8080`. For real deployments, put a TLS-terminating reverse proxy in front and set `IdentityServer__Authority` to the public https URL. Full self-host guide (reverse proxy, external SQL, backups, upgrades, multi-replica): [`docs/DOCKER.md`](docs/DOCKER.md).

````

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs(readme): add Docker quick-start + GHCR badge"
```

---

## Task 12: Open the PR

**Files:** none

- [ ] **Step 1: Push the feature branch**

Run: `git push origin feature/docker-distribution`

- [ ] **Step 2: Open the PR**

Run:

```bash
gh pr create --base develop --title "feat(docker): docker distribution + compose bundle" --body "$(cat <<'EOF'
## Summary

Adds Docker as a second, equal distribution channel for Timinute alongside the existing per-OS release tarballs. Implements the design in `docs/superpowers/specs/2026-05-15-docker-distribution-design.md`.

- **`Dockerfile`** — multi-stage build on `aspnet:10.0` Debian, non-root `app` user, cross-arch via `$BUILDPLATFORM` + `$TARGETARCH`
- **`docker-compose.yml`** — app + SQL Server 2025, named volumes for `/keys` + SQL data
- **`.env.example`** — required secrets / config with `${VAR:?}` guards in compose
- **`Program.cs`** — explicit `ForwardedHeaders` middleware (loopback-only auto-default doesn't work behind a containerized reverse proxy) and config-gated `Database.Migrate()` at startup (`DatabaseMigrationOnStartup`, default `true`)
- **`.github/workflows/docker-publish.yml`** — multi-arch build + push to `ghcr.io/jame581/timinute` on `v*` tag (publishes `:latest`, `:X.Y.Z`, `:X.Y`, `:X`) and `develop` push (`:develop` preview channel); `workflow_dispatch` for manual runs
- **`docs/DOCKER.md`** — full self-host guide
- **`README.md`** — quick-start + GHCR badge

Existing `release.yml` flow is **unchanged**. Existing `dotnet run` dev workflow is **unchanged at the network layer** (`UseForwardedHeaders` is a no-op without forwarded headers); the only dev-behavior shift is `dotnet run` now auto-applies pending EF migrations, which is idempotent — flip `DatabaseMigrationOnStartup=false` to opt out.

## Test plan

- [ ] `dotnet build Timinute.sln --configuration Release` succeeds
- [ ] `dotnet test Timinute.sln --configuration Release --no-build` — all existing tests pass
- [ ] `dotnet run` locally — app starts, migrations apply idempotently, landing page renders at `https://localhost:7047`
- [ ] `docker build -t timinute:dev .` succeeds on host native arch
- [ ] `docker compose up -d` on a clean host → app reachable at `http://localhost:8080`, register a user, track a task, see it persisted
- [ ] `docker compose down && docker compose up -d` → no data loss
- [ ] `docker compose down -v && docker compose up -d` → fresh DB, migrations apply cleanly
- [ ] Behind Caddy with real https cert → OIDC login works end-to-end
- [ ] Restart container → `/keys` survives → users stay logged in
- [ ] Pull image on `linux/arm64` host → starts and serves correctly
- [ ] `gh workflow run docker-publish.yml` from feature branch → publishes to GHCR with both arches

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Address review feedback**

For any review comment, fix in a new commit on `feature/docker-distribution`, push, let the reviewer re-look.

- [ ] **Step 4: Merge**

After approval and green CI, merge via the GitHub UI (squash or merge — match the project's existing convention; PRs #40, #41, #42 used "Merge pull request" style, so use the merge-commit option).

- [ ] **Step 5: Verify the `develop` push triggered the workflow**

After merge, the `docker-publish.yml` workflow auto-runs (push to `develop` is a trigger). Watch it:

```bash
gh run list --workflow=docker-publish.yml --branch develop --limit 1
```

Expected: green run; `:develop` tag now points at the merged image at GHCR.

---

## Task 13: Post-merge — update the feature roadmap

**Files:**
- Modify: `docs/superpowers/plans/feature-roadmap.md`

This task runs after Task 12 merges. It's bookkeeping for the project's roadmap doc.

- [ ] **Step 1: Update the "Recently shipped" section**

In `docs/superpowers/plans/feature-roadmap.md`, find the section `## Recently shipped (v2.1, 2026-04-29)` and add a new section above it (or update the heading if appropriate) — the format is one section per release:

```markdown
## Recently shipped (post-v2.1, develop)

| Feature | PRs |
|---------|-----|
| Docker distribution (image + compose bundle, GHCR multi-arch publish) | #<PR-number-from-Task-12> |

## Recently shipped (v2.1, 2026-04-29)
…(existing content unchanged)…
```

- [ ] **Step 2: Update the `_Last reviewed:_` line**

At the top of the file, update `_Last reviewed: 2026-04-29 — after v2.1 release (Settings cluster + dark mode)._` to:

```markdown
_Last reviewed: 2026-05-15 — after Docker distribution merge._
```

- [ ] **Step 3: Commit directly to develop (housekeeping)**

This is doc-only and follows the project's "direct merge for housekeeping" policy noted in the roadmap's P1-follow-ups table.

```bash
git checkout develop
git pull
git add docs/superpowers/plans/feature-roadmap.md
git commit -m "docs(roadmap): docker distribution shipped"
git push
```

---

## Out-of-scope notes (do not implement)

The following are explicitly out-of-scope per the spec; do NOT add them to this implementation:

- Hadolint Dockerfile lint job in `build_test.yml`
- Cosign image signing / SBOM attestation
- Separate migration-runner container
- Helm chart / Kubernetes manifests
- Docker Hub mirror
- `PathBase` / subpath hosting
- SQLite / Postgres provider support
- `linux/arm/v7`
- Channel-pinning UX beyond what semver gives us

Each of the above can be its own follow-up spec if user demand surfaces.
