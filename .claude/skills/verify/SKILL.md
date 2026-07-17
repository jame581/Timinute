---
name: verify
description: Use when a change to Timinute needs to be confirmed working in the real running app — launching the app locally, logging in, and driving the affected pages end-to-end (not just tests).
---

# Verify a change in the running app

## Launch

1. Prereqs: .NET 10 SDK, Docker Desktop running, trusted dev cert (`dotnet dev-certs https --check --trust`).
2. SQL container: check `docker ps -a --filter name=timinute.sql.server` FIRST. **`scripts\SetupDockerSql.ps1` force-removes an existing container and wipes its data** — only run it when the container is absent. Then `.\scripts\MigrateDatabase.ps1` (idempotent).
3. `dotnet build Timinute.sln` to fail fast, then run in a background shell:
   ```powershell
   dotnet run --project Timinute/Server/Timinute.Server.csproj
   ```
   Wait for `Now listening on: https://localhost:7047`. Smoke: `https://localhost:7047` (landing) and `/swagger` (API + IdentityServer up).

## Log in

- Seed users exist (`test1@email.com` etc.) but their plaintext password is NOT recorded in the repo — only hashes. **Do not brute-guess: lockout triggers after 5 failed attempts (5 min).** Ask the user for it once, or register a fresh user via the Register page (≥8 chars with upper + lower + digit and ≥4 unique chars). No email sender is registered, so after registering, click the confirmation link shown on the post-register page — `RequireConfirmedAccount` is on, and login is blocked until you do. A fresh user has no data — create a project on `/projectmanager` first.

## Exercise the change

- Drive the app with the claude-in-chrome tools; keep the network log open so API calls and status codes are inspectable.
- Exercise **every entry point** of the changed component — shared dialogs/components are typically reachable from more than one page (e.g. the task edit dialog opens from both `/trackedtasks` and `/scheduler`, with separate result-handling code at each call site).
- Prove persistence, not client state: after saving, hard-refresh (Ctrl+F5) and confirm values survived. For DB-level certainty:
  ```powershell
  docker exec timinute.sql.server /opt/mssql-tools18/bin/sqlcmd -U sa -P "TiminuteAdmin." -No -Q "<query>" -d Timinute
  ```
- Also hit the negative paths (validation messages block save) and, for deletes, the undo toast → restore.

## Wrap up

Check the `dotnet run` console for server exceptions and the browser console for Blazor errors. Kill the background `dotnet run`; leave the SQL container running.
