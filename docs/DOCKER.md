# Running Timinute with Docker

Timinute publishes a multi-arch Docker image (`linux/amd64` + `linux/arm64`) to GitHub Container Registry. The bundled `docker-compose.yml` starts the app alongside a SQL Server container for a five-minute self-host experience. For production you typically replace the bundled SQL with your own and run Timinute behind a reverse proxy that handles TLS.

## Quick start

```bash
git clone https://github.com/jame581/Timinute.git
cd Timinute
cp .env.example .env
# edit .env: set MSSQL_SA_PASSWORD and IdentityServer__Authority
docker compose up -d
```

The app comes up on `http://localhost:8080` (configurable via `TIMINUTE_PORT`). For production, put a reverse proxy in front with a real TLS certificate and set `IdentityServer__Authority` to the public https URL (e.g. `https://timinute.example.com`).

## Image tags

| Tag                 | Channel        | When it advances                          |
|---------------------|----------------|-------------------------------------------|
| `latest`            | Stable release | On every `v*` git tag                     |
| `2.1.0`, `2.1`, `2` | Pinned release | On the corresponding `v*` git tag         |
| `develop`           | Preview        | On every push to the `develop` branch     |
| `@sha256:...`       | Digest pin     | Never moves — recommended for production  |

Pin by digest in production to get deterministic, immutable deployments:

```yaml
image: ghcr.io/jame581/timinute@sha256:abc123...
```

## Configuration reference

All settings flow through ASP.NET Core's hierarchical configuration — environment variables with `__` between segments override `appsettings.json`. The most-frequently-overridden ones:

| Variable                                     | Default (in container)    | Purpose                                                   |
|----------------------------------------------|---------------------------|-----------------------------------------------------------|
| `ConnectionStrings__DefaultConnection`       | _(set in compose)_        | SQL Server connection string                              |
| `IdentityServer__Authority`                  | `https://localhost:7047`  | OIDC issuer URL — must exactly match the browser URL      |
| `IdentityServer__KeyManagement__KeyPath`     | `/keys`                   | Directory for Duende signing keys (rarely changed)        |
| `DataProtection__KeyPath`                    | `/keys/data-protection`   | Directory for ASP.NET data protection keys (rarely changed) |
| `DatabaseMigrationOnStartup`                 | `true`                    | Auto-apply EF migrations on container start               |
| `ASPNETCORE_ENVIRONMENT`                     | `Production` (in compose) | Standard ASP.NET environment flag                         |
| `ASPNETCORE_URLS`                            | `http://+:8080`           | Listen address inside the container                       |
| `TrashRetention__Days`                       | `30`                      | Soft-delete retention days before hard-purge              |
| `TrashRetention__PurgeIntervalHours`         | `24`                      | How often the background purge service runs               |

### `IdentityServer__Authority` — the most important setting

This value must **exactly** match the URL the user's browser uses to reach the app — scheme, host, port (if non-standard), no trailing slash. Duende IdentityServer embeds this URL into every JWT it issues, and the app validates it on each request.

| Scenario                              | Correct value                         |
|---------------------------------------|---------------------------------------|
| Behind a reverse proxy with TLS       | `https://timinute.example.com`        |
| Local smoke test, no proxy            | `http://localhost:8080`               |
| Custom port, no TLS                   | `http://localhost:9000`               |

A mismatch between `IdentityServer__Authority` and the actual browser URL produces `Invalid redirect_uri` errors from Duende and prevents any user from logging in. See [Troubleshooting](#troubleshooting) for the full diagnostic.

## Volumes

| Volume          | Mount path inside container | Holds                                                       |
|-----------------|-----------------------------|------------------------------------------------------------|
| `timinute-data` | `/var/opt/mssql`            | SQL Server data and logs (bundled DB only)                  |
| `timinute-keys` | `/keys`                     | IdentityServer signing keys AND ASP.NET data protection keys |

The `timinute-keys` volume holds two subdirectories:

- `/keys` — Duende IdentityServer JWT signing keys
- `/keys/data-protection` — ASP.NET Core data protection keys, which encrypt cookies, antiforgery tokens, and Duende's persisted signing keys at rest

**Both subdirectories live in the same named volume.** Losing `timinute-keys` triggers two simultaneous failures: every user is immediately logged out (signing keys rotated), and the app throws `CryptographicException: The key {guid} was not found in the key ring` on the next request (the data protection keys that encrypted the old signing keys are also gone). Always back up this volume before any destructive operation.

Losing `timinute-data` means losing all user data.

### Backing up

```bash
# Snapshot SQL data via sqlcmd inside the db container
docker compose exec db bash -c \
  "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -No \
   -Q 'BACKUP DATABASE Timinute TO DISK = N\"/var/opt/mssql/data/Timinute.bak\" WITH FORMAT, INIT'"

docker cp "$(docker compose ps -q db):/var/opt/mssql/data/Timinute.bak" \
  "./Timinute-$(date +%Y%m%d).bak"

# Snapshot signing and data-protection keys
docker run --rm \
  -v timinute-keys:/keys \
  -v "$(pwd)":/out \
  alpine tar -czf "/out/timinute-keys-$(date +%Y%m%d).tar.gz" -C /keys .
```

PowerShell users: replace `$(date +%Y%m%d)` with `$(Get-Date -Format yyyyMMdd)` in the snippets above.

To restore the keys volume from a backup:

```bash
docker run --rm \
  -v timinute-keys:/keys \
  -v "$(pwd)":/backup \
  alpine tar -xzf /backup/timinute-keys-20260515.tar.gz -C /keys
```

## Reverse proxy

The container speaks plain HTTP on port 8080. For any deployment where users connect over the internet, put a TLS-terminating reverse proxy in front. The app's `ForwardedHeaders` middleware is configured to trust `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host` from any upstream inside the Docker network, so the correct scheme reaches IdentityServer.

Three examples:

### Caddy (most ergonomic)

Caddy auto-provisions a Let's Encrypt certificate and forwards the necessary headers out of the box.

```caddyfile
timinute.example.com {
    reverse_proxy timinute-app:8080
}
```

Add Caddy as a third service in compose or run it as a separate stack on a shared external Docker network.

### nginx

```nginx
server {
    listen 443 ssl http2;
    server_name timinute.example.com;
    ssl_certificate     /etc/letsencrypt/live/timinute.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/timinute.example.com/privkey.pem;

    location / {
        proxy_pass         http://timinute-app:8080;
        proxy_set_header   Host               $host;
        proxy_set_header   X-Real-IP          $remote_addr;
        proxy_set_header   X-Forwarded-For    $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto  $scheme;
        proxy_set_header   X-Forwarded-Host   $host;
    }
}
```

The `X-Forwarded-Proto $scheme;` line is required. Without it the app sees an `http://` issuer even when users connect over HTTPS, causing OIDC validation to fail.

### Traefik

Add these labels to the `app` service in `docker-compose.yml`:

```yaml
labels:
  - "traefik.enable=true"
  - "traefik.http.routers.timinute.rule=Host(`timinute.example.com`)"
  - "traefik.http.routers.timinute.entrypoints=websecure"
  - "traefik.http.routers.timinute.tls.certresolver=letsencrypt"
  - "traefik.http.services.timinute.loadbalancer.server.port=8080"
```

Traefik sets forwarded headers automatically when using its standard HTTPS entrypoint.

## External SQL Server

To use an existing SQL Server instead of the bundled one:

1. Comment out the entire `db` service block in `docker-compose.yml`.
2. Remove the `depends_on` block from the `app` service (it references `db`).
3. In `.env`, set a full connection string and pass it through compose. The simplest approach is to replace the inline `ConnectionStrings__DefaultConnection` value in compose:

   ```yaml
   ConnectionStrings__DefaultConnection: ${ConnectionStrings__DefaultConnection}
   ```

   Then add to `.env`:

   ```bash
   ConnectionStrings__DefaultConnection=Server=your-sql-host,1433;Database=Timinute;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=true
   ```

4. `MSSQL_SA_PASSWORD` is no longer used; remove it from `.env`.
5. Ensure the target database exists before first start. `DatabaseMigrationOnStartup=true` will create tables but not the database itself.

## Upgrading

```bash
docker compose pull
docker compose up -d
```

Schema migrations apply automatically on container start (`DatabaseMigrationOnStartup=true`). Schema changes are forward-only: once a newer image migrates the database, you cannot downgrade to an older image. Back up `timinute-data` before upgrading.

## Disabling auto-migrate

For multi-replica deployments or DBA-managed schemas, disable the automatic migration on the app replicas and run a separate one-shot migration container before rolling new app containers:

```bash
# Step 1: apply migrations with a short-lived container
docker run --rm \
    -e ConnectionStrings__DefaultConnection="Server=your-sql-host,1433;Database=Timinute;..." \
    -e DatabaseMigrationOnStartup=true \
    ghcr.io/jame581/timinute:2.2.0
# exits 0 once migrations are done

# Step 2: start (or roll) app replicas with migration disabled
docker compose up -d
# docker-compose.yml should include: DatabaseMigrationOnStartup=false
```

This prevents concurrent migration attempts when multiple containers start simultaneously.

## Cookie and session behavior

The app uses SameSite=Lax cookies with `Secure` set to match the request scheme (`SameAsRequest`). This means:

- Behind a TLS-terminating reverse proxy (production): cookies are `Secure`, HTTPS-only.
- HTTP-only smoke test or local dev: cookies work without `Secure`, which browsers allow for `localhost`.

This policy is transparent to most users. It is documented here for operators who need to audit cookie behavior or who are customizing the authentication stack.

## Troubleshooting

**"Invalid redirect_uri: http://..." / OIDC login never completes**

The `IdentityServer__Authority` value does not match the URL the browser is using. Check three things:

1. `IdentityServer__Authority` in `.env` matches the exact scheme+host the browser hits (e.g. `https://timinute.example.com`, not `http://`).
2. No trailing slash on the value.
3. If behind a reverse proxy, confirm the proxy is running and routing correctly before diagnosing the app.

For a local no-TLS smoke test, the correct value is `http://localhost:8080`, not `https://`.

**Login form submits and "User logged in" appears in logs, but the app redirects back unauthenticated**

This is a cookie SameSite/Secure policy mismatch. Symptoms: the login page accepts credentials and the server log shows `User logged in`, but the browser ends up back on the login page on every redirect. Diagnostic steps:

1. Open browser dev tools, go to Application > Cookies. After login, check whether a `.AspNetCore.Cookies` (or similar) cookie is present.
2. If a cookie with `Secure` flag is being set but the request is over plain HTTP, the browser silently drops it.
3. The current image uses `SameAsRequest` so Secure follows the scheme — this should not occur with the shipped configuration. If you have customized the cookie policy, revert to the defaults and verify.

**"Invalid issuer" / JWT validation fails after login**

The OIDC client (Blazor WASM) validates the `iss` claim in the JWT against the configured authority. If `IdentityServer__Authority` changed between container restarts, old tokens will fail validation until they expire. Force re-login by clearing browser cookies for the site.

**All users are logged out after `docker compose down && docker compose up`**

The `timinute-keys` volume was not persisted. IdentityServer regenerated its signing keys on restart, invalidating every previously issued token. Confirm the volume exists and is mounted:

```bash
docker volume ls | grep timinute-keys
docker inspect timinute_timinute-keys
```

If the volume was deleted (e.g. via `docker compose down -v`), it cannot be recovered without a backup. All users must log in again. This is expected behavior for a clean teardown — use `docker compose down` (without `-v`) for routine restarts.

**SQL container stuck in `starting` or `unhealthy` (~30–60s on first boot)**

Normal. SQL Server initializes `master`, `tempdb`, and system databases on first start. The healthcheck has a `start_period: 30s` for this. If the container stays `unhealthy` past 5 minutes, check:

```bash
docker compose logs db
```

Common causes: `MSSQL_SA_PASSWORD` does not satisfy SQL Server's complexity rules (8+ characters, mixed case, at least one digit, at least one symbol); or the `timinute-data` volume was initialized with a different password (recreate with `docker compose down -v`, noting this destroys all data).

**App starts but immediately exits — "Cannot open database" or migration failure**

The `app` container uses `depends_on: db: condition: service_healthy`, so it will not start until the SQL healthcheck passes. If you see this immediately after `docker compose up -d`, check whether the `db` service ever became healthy:

```bash
docker compose ps
docker compose logs db --tail=50
```

**`X-Forwarded-Proto` not honored — the app sees `http://` even behind an HTTPS proxy**

Confirm your reverse proxy is setting the header. nginx requires the explicit `proxy_set_header X-Forwarded-Proto $scheme;` line — it is not inherited from any default. Caddy and Traefik set it automatically. You can verify by adding `app.Use(async (ctx, next) => { Console.WriteLine(ctx.Request.Scheme); await next(); });` temporarily to `Program.cs`, or by checking Duende logs for the issuer it computed.

**"CryptographicException: The key {guid} was not found in the key ring"**

This means the data protection keys that were used to encrypt Duende's persisted signing keys are missing. It happens when the `timinute-keys` volume is recreated while Duende still has references to signing keys it encrypted with the old data protection keys. The fix is to restore both from backup, or to accept that existing sessions are invalid and bring up a fresh stack:

```bash
docker compose down -v
docker compose up -d
```

All users will need to log in again after this.
