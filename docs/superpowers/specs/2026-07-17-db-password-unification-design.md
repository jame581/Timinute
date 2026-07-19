# DB password unification — one env var for container and app

_Date: 2026-07-17_
_Status: design approved, pending implementation_

## Purpose

Make a single environment variable, `MSSQL_SA_PASSWORD`, the source of truth for
the SQL Server SA password across **both** the local-dev workflow (`dotnet run` +
the `SetupDockerSql.ps1` container on port 44555) and the docker-compose stack.
Today the two worlds are internally consistent but diverge from each other: local
dev is pinned to the literal `TiminuteAdmin.` in three places, while compose reads
`MSSQL_SA_PASSWORD` from `.env`. This is a developer-experience/config change only
— no behavioural change for users and, critically, **no change at all for anyone
who sets nothing**.

## Context

`TiminuteAdmin.` (the well-known local dev password — the README already notes
`appsettings.json` ships it) is currently hardcoded in:

| Location | Role |
|---|---|
| `scripts/SetupDockerSql.ps1` | creates the local dev container (`-e MSSQL_SA_PASSWORD=TiminuteAdmin.`, port 44555) |
| `Timinute/Server/appsettings.json` | `ConnectionStrings:DefaultConnection` the app uses |
| `.claude/skills/verify/SKILL.md` | `sqlcmd` example for DB-level checks |

The env-driven world:

| Location | Role |
|---|---|
| `.mcp.json` | `DB_PASSWORD` = `${MSSQL_SA_PASSWORD:-TiminuteAdmin.}` (already unified) |
| `docker-compose.yml` + `.env` | `MSSQL_SA_PASSWORD` (required; `.env` currently `Local.Test-Pw-123!`) |

`Program.cs:25` reads the connection string with
`builder.Configuration.GetConnectionString("DefaultConnection")`; compose supplies
its own already-substituted `ConnectionStrings__DefaultConnection`, so by the time
the app reads it the password is resolved.

## Scope

### In scope
- `scripts/SetupDockerSql.ps1`: container password from `$env:MSSQL_SA_PASSWORD`, default `TiminuteAdmin.`.
- `Timinute/Server/Program.cs`: guarded substitution so the running app honors `MSSQL_SA_PASSWORD` for local dev.
- `.claude/skills/verify/SKILL.md`: `sqlcmd` example reads the env var with the same default.
- Docs: a short "one env var drives the DB password" note in `README.md` and `CLAUDE.md`.

### Out of scope
- `.mcp.json` — already done.
- Changing the default password value, `appsettings.json`'s committed value, or the local container port.
- Aligning `.env`'s value to `TiminuteAdmin.` (it's a per-user gitignored file).

## Design

**`SetupDockerSql.ps1`** — resolve once, keep the default:
```powershell
$saPassword = if ($env:MSSQL_SA_PASSWORD) { $env:MSSQL_SA_PASSWORD } else { "TiminuteAdmin." }
# -e "MSSQL_SA_PASSWORD=$saPassword"
```

**`Program.cs`** — the only code change. After reading the connection string,
override the password from the env var **only when the string still carries the
built-in dev default** — using `SqlConnectionStringBuilder` rather than string
replacement:
```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var saPassword = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
if (!string.IsNullOrEmpty(saPassword) && !string.IsNullOrEmpty(connectionString))
{
    var csb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
    if (csb.Password == "TiminuteAdmin.")   // built-in dev default only
    {
        csb.Password = saPassword;
        connectionString = csb.ConnectionString;
    }
}
```
The `Password == "TiminuteAdmin."` guard is the safety boundary. A production
`ConnectionStrings__DefaultConnection` override, or the compose connection string
(already filled with the real password by docker-compose), has a different
password and is therefore never touched. `appsettings.json` stays `TiminuteAdmin.`
and doubles as the sentinel the guard matches.

**`verify` skill** — the `sqlcmd` line becomes
`-P "$($env:MSSQL_SA_PASSWORD ?? 'TiminuteAdmin.')"` (PowerShell 7 null-coalescing).

## Backward compatibility

- **Set-nothing users:** `MSSQL_SA_PASSWORD` unset → `SetupDockerSql` default `TiminuteAdmin.`, `Program.cs` performs no substitution, `appsettings.json` unchanged. Byte-for-byte identical to today.
- **Compose users:** unchanged — compose already sources `MSSQL_SA_PASSWORD` and substitutes it before the app reads the string; the `Program.cs` guard skips the (non-default) password.
- **Edge case (documented):** a user who exports `MSSQL_SA_PASSWORD` in their interactive shell to a value different from their existing `TiminuteAdmin.` container will have `dotnet run` attempt the env password against the old container and fail. Rare, because the compose `.env` password is normally only visible to the compose process, not the interactive shell. This is the same "env override wins" tradeoff already accepted for `.mcp.json`. Fix: re-run `SetupDockerSql.ps1` to recreate the container on the new password, or unset the var.

## Testing

No automated-test impact — `Server.Tests` use `TestHelper.GetDefaultApplicationDbContext`
(InMemory) / `GetSqliteApplicationDbContext`, never `DefaultConnection`. Manual
verification:
1. Var unset → `dotnet run` connects (password `TiminuteAdmin.`), app reaches DB.
2. `SetupDockerSql.ps1` then `dotnet run` with `MSSQL_SA_PASSWORD` set to a fresh
   strong password → container created with it, app connects.
3. Confirm the built solution still starts and Swagger loads.

## Open questions

None.
