# UTC Normalization: DateTime → DateTimeOffset Migration

## Goal

Migrate all date/time handling from `DateTime` to `DateTimeOffset` across models, DTOs, controllers, and tests to eliminate timezone ambiguity.

## Problem

`DateTime` with `DateTimeKind.Unspecified` + `.ToUniversalTime()` produces different results depending on server timezone. 9 risky conversion calls across 3 controllers, 3 `DateTime.Now` in tests, 35+ seed data entries with ambiguous `DateTimeKind`.

## Scope

Single PR: models, DTOs, controllers, tests, DB migration, seed data. All in one cut.

## Model Changes

**TrackedTask:**
- `StartDate`: `DateTime` → `DateTimeOffset`
- `EndDate`: `DateTime?` → `DateTimeOffset?`

No changes to `Project`, `ApplicationUser`, or Duration (remains `TimeSpan`).

## DTO Changes

**CreateTrackedTaskDto:**
- `StartDate`: `DateTime?` → `DateTimeOffset?`

**UpdateTrackedTaskDto:**
- `StartDate`: `DateTime?` → `DateTimeOffset?`
- `EndDate`: `DateTime?` → `DateTimeOffset?`

**TrackedTaskDto:**
- `StartDate`: `DateTime` → `DateTimeOffset`
- `EndDate`: `DateTime?` → `DateTimeOffset?`

**ProjectDataItemsPerMonthDto:**
- `Time`: `DateTime` → `DateTimeOffset`

## Controller Changes

**Key principle:** `DateTimeOffset.ToUniversalTime()` is always correct because the offset is explicit, unlike `DateTime.ToUniversalTime()` which guesses based on `Kind`.

**TrackedTaskController:**
- `CreateTrackedTask`: `StartDate.Value.ToUniversalTime()` — now safe with DateTimeOffset
- `UpdateTrackedTask`: same pattern, EndDate validation unchanged

**AnalyticsController:**
- Replace `new DateTime(Year, Month, 1).ToUniversalTime()` with `new DateTimeOffset(Year, Month, 1, 0, 0, 0, TimeSpan.Zero)`

**ExportController:**
- Query params `from`/`to` become `DateTimeOffset?`
- `.ToUniversalTime()` calls remain but are now always correct

## DB Migration

EF Core migration: `ALTER COLUMN` from `datetime2` to `datetimeoffset`. SQL Server treats existing values as UTC (+00:00). No data loss.

## Seed Data

All `new DateTime(y, m, d, h, m, s)` → `new DateTimeOffset(y, m, d, h, m, s, TimeSpan.Zero)` in:
- `ApplicationDbContext.FillDataToDB` (7 entries)
- `TestHelper.FillInitData` (16 entries)
- `TestHelper.FillAnalyticsData` (11 entries)

## Test Changes

- All `DateTime.Now` → `DateTimeOffset.UtcNow`
- All `DateTime.UtcNow` → `DateTimeOffset.UtcNow`
- All `new DateTime(...)` in test data → `new DateTimeOffset(..., TimeSpan.Zero)`
- Assertions comparing dates updated to `DateTimeOffset`

## AutoMapper

No mapping profile changes needed — AutoMapper handles `DateTimeOffset` ↔ `DateTimeOffset` automatically.

## Client Compatibility

Radzen DatePicker outputs `DateTime`. `System.Text.Json` serializes to ISO 8601. Server `DateTimeOffset` binder parses with offset if provided, otherwise treats as local. Client sends local time with offset — server converts to UTC for storage.

## Client Compatibility — RadzenDatePicker Limitation

**RadzenDatePicker does not support `DateTimeOffset`.** It binds to `DateTime`/`DateTime?` only.

The client-side model `Timinute/Client/Models/TrackedTask.cs` must remain `DateTime` for Radzen binding. The conversion happens at the boundary:
- **Inbound (DTO → Client model):** `TrackedTask(TrackedTaskDto dto)` constructor calls `.ToLocalTime()` — with `DateTimeOffset`, use `.LocalDateTime` instead
- **Outbound (Client → API):** JSON serialization of `DateTime` produces ISO 8601. Server binds to `DateTimeOffset` with the appropriate offset.

The client model `DateTime.Now` usages (`AddTrackedTask.razor.cs`, `TrackTaskTime.razor.cs`, `Dashboard.razor.cs`) are correct for the client — local time is intentional for UI display. The server handles UTC conversion.

## Files to Modify

**Server Models:**
- `Timinute/Server/Models/TrackedTask.cs`

**Shared DTOs:**
- `Timinute/Shared/Dtos/TrackedTask/CreateTrackedTaskDto.cs`
- `Timinute/Shared/Dtos/TrackedTask/UpdateTrackedTaskDto.cs`
- `Timinute/Shared/Dtos/TrackedTask/TrackedTaskDto.cs`
- `Timinute/Shared/Dtos/Dashboard/ProjectDataItemsPerMonthDto.cs`

**Server Controllers:**
- `Timinute/Server/Controllers/TrackedTaskController.cs`
- `Timinute/Server/Controllers/AnalyticsController.cs`
- `Timinute/Server/Controllers/ExportController.cs`

**Database:**
- `Timinute/Server/Data/ApplicationDbContext.cs` (seed data)
- New EF migration

**Client Models (DateTime stays, update conversion):**
- `Timinute/Client/Models/TrackedTask.cs` (update constructor: `.ToLocalTime()` → `.LocalDateTime`)
- `Timinute/Client/Models/Dashboard/ProjectDataItemsPerMonth.cs` (stays DateTime — display only)

**Tests:**
- `Timinute/Server.Tests/Helpers/TestHelper.cs`
- `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs`
- `Timinute/Server.Tests/Controllers/AnalyticsControllerTest.cs`
- `Timinute/Server.Tests/Controllers/ExportControllerTest.cs`
- `Timinute/Server.Tests/Repositories/TrackedTaskRepositoryTest.cs`

## Test Plan

- All 60 existing tests must pass after migration
- No new tests needed — existing tests validate the same behavior with correct types
- Verify DB migration applies cleanly
