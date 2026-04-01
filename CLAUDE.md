# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Timinute is a time tracker and timesheet app built with **Blazor WebAssembly (hosted)** on **.NET 10.0**. It uses a client-server architecture with a shared DTO library.

## Build & Run Commands

```bash
# Build the entire solution
dotnet build Timinute.sln

# Run the server (also serves the Blazor client)
dotnet run --project Timinute/Server/Timinute.Server.csproj

# Run all tests
dotnet test

# Run a single test by name
dotnet test --filter "FullyQualifiedName~ProjectControllerTest.GetAllProjects"

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> --project Timinute/Server/Timinute.Server.csproj

# Apply migrations
dotnet ef database update --project Timinute/Server/Timinute.Server.csproj
```

**Database setup** requires Docker Desktop. Run `scripts/SetupDockerSql.ps1` to create a SQL Server 2019 container (port 44555), then `scripts/MigrateDatabase.ps1` to apply migrations.

## Architecture

**Three-project structure** inside `Timinute/`:
- **Server** — ASP.NET Core Web API with Identity + Duende IdentityServer (v7) authentication, EF Core with SQL Server, Swagger at `/swagger`. Uses a policy scheme to route JWT Bearer for API requests and cookies for Identity UI.
- **Client** — Blazor WebAssembly SPA using Radzen component library, PWA-enabled
- **Shared** — DTOs shared between client and server (separate DTOs for Create/Update operations)

**Test project:** `Timinute/Server.Tests` — xUnit + Moq + EF Core InMemory provider. Uses `ControllerTestBase<T>` base class for controller tests.

### Backend Patterns

- **Generic Repository + Factory**: `IRepository<T>` / `BaseRepository<T>` with `RepositoryFactory` for DI. Supports dynamic LINQ filtering, pagination, and sorting.
- **AutoMapper**: `MappingProfile.cs` defines all model-to-DTO mappings.
- **Controllers** receive `IRepositoryFactory`, `IMapper`, `ILogger` via constructor injection and extract user ID from JWT claims.

### Domain Model

`ApplicationUser` → owns many `Project` → each has many `TrackedTask`. TrackedTasks also link directly to the user.

### Key Configuration

- Server URLs: `https://localhost:7047` / `http://localhost:5047`
- DB connection: `Server=127.0.0.1,44555;Database=Timinute;User Id=sa`
- Seed data includes test users (test1-3@email.com) and sample tracked tasks

## Docs

Planning and design specs live in `docs/superpowers/`:
- `plans/` — modernization roadmap, UI redesign spec, feature roadmap
- `specs/` — detailed design specs (e.g., P0 validation & authorization)
