---
name: add-migration
description: Use when the user runs /add-migration <MigrationName> to generate, verify, and apply an EF Core migration in Timinute after an entity or model change.
disable-model-invocation: true
---

# Add EF Core Migration

Generate, verify, and locally apply a migration for `Timinute.Server`. The argument is the migration name (PascalCase, e.g. `AddTaskNotes`). If no name was given, ask for one — never invent it.

## Steps

1. **Precondition** — confirm the model change is in a *server* entity (`Timinute/Server/Models/`) and the solution builds: `dotnet build Timinute.sln`. Client models and Shared DTOs do not affect the EF model.

2. **Generate** — the script computes paths relative to `scripts/`, so run it from there:
   ```powershell
   cd scripts; .\AddMigration.ps1 -name <MigrationName>; cd ..
   ```

3. **Verify the generated migration** before applying:
   - New `<timestamp>_<MigrationName>.cs` + `.Designer.cs` under `Timinute/Server/Data/Migrations/`.
   - `git diff` on `ApplicationDbContextModelSnapshot.cs` must show ONLY the intended change. Unrelated diffs = model drift — stop and investigate, do not apply.
   - Column nullability/length must match the entity's annotations.
   - If the change touches a seeded table (`HasData` in `ApplicationDbContext`): non-nullable columns without defaults break the SQLite test helper (`EnsureCreatedAsync` builds schema from the model + seed). Update the seed in the same commit.

4. **Apply locally** — check the container first: `docker ps -a --filter name=timinute.sql.server`. Run `.\scripts\SetupDockerSql.ps1` ONLY if it is absent — the script force-removes an existing container and wipes its data (if it exists but is stopped, `docker start timinute.sql.server`). Then:
   ```powershell
   cd scripts; .\MigrateDatabase.ps1; cd ..
   ```
   Both scripts run `dotnet tool install --global dotnet-ef` unconditionally; a non-zero exit saying the tool is already installed is expected noise, not a failure.

5. **Test** — targeted first, then full:
   ```powershell
   dotnet test Timinute/Server.Tests/Timinute.Server.Tests.csproj --filter "FullyQualifiedName~<AffectedEntity>"
   dotnet test Timinute.sln
   ```
   Any new query over the changed columns that must translate to SQL needs a test on `TestHelper.GetSqliteApplicationDbContext`, not InMemory.

6. **Docs** — if the migration alters `AspNetUsers` or drops/renames tables, add a migration note to README's "Production deployment" section (see the v2.0/v2.1 notes there for the format). Docker deployments auto-migrate via `DatabaseMigrationOnStartup=true`; call out anything an operator must do manually.

7. **Commit** the entity change, migration, Designer, and snapshot together. Remind the user that the full DTO chain (Shared DTO → `MappingProfile.cs` → Client model) is a separate step if the new column must reach the UI.
