# UTC Normalization: DateTime → DateTimeOffset Implementation Plan

> **Status:** ✅ Shipped — merged via PR #28 on 2026-04-01. All `TrackedTask` date columns and DTO properties use `DateTimeOffset`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate all date/time handling from `DateTime` to `DateTimeOffset` to eliminate timezone ambiguity.

**Architecture:** Change the `TrackedTask` model and all related DTOs from `DateTime` to `DateTimeOffset`. Update controllers to use `DateTimeOffset` naturally (no more `.ToUniversalTime()` guessing). Update seed data and tests. Generate EF migration. Client model stays `DateTime` for RadzenDatePicker compatibility, converts at DTO boundary.

**Tech Stack:** .NET 10, EF Core 10, SQL Server (datetime2 → datetimeoffset), xUnit

---

### Task 1: Update TrackedTask model and DTOs

**Files:**
- Modify: `Timinute/Server/Models/TrackedTask.cs`
- Modify: `Timinute/Shared/Dtos/TrackedTask/TrackedTaskDto.cs`
- Modify: `Timinute/Shared/Dtos/TrackedTask/CreateTrackedTaskDto.cs`
- Modify: `Timinute/Shared/Dtos/TrackedTask/UpdateTrackedTaskDto.cs`
- Modify: `Timinute/Shared/Dtos/Dashboard/ProjectDataItemsPerMonthDto.cs`

- [ ] **Step 1: Update TrackedTask model**

In `Timinute/Server/Models/TrackedTask.cs`, change:
```csharp
public DateTime StartDate { get; set; }
public DateTime? EndDate { get; set; }
```
To:
```csharp
public DateTimeOffset StartDate { get; set; }
public DateTimeOffset? EndDate { get; set; }
```

- [ ] **Step 2: Update TrackedTaskDto**

In `Timinute/Shared/Dtos/TrackedTask/TrackedTaskDto.cs`, change:
```csharp
public DateTime StartDate { get; set; }
public DateTime? EndDate { get; set; }
```
To:
```csharp
public DateTimeOffset StartDate { get; set; }
public DateTimeOffset? EndDate { get; set; }
```

- [ ] **Step 3: Update CreateTrackedTaskDto**

In `Timinute/Shared/Dtos/TrackedTask/CreateTrackedTaskDto.cs`, change:
```csharp
public DateTime? StartDate { get; set; }
```
To:
```csharp
public DateTimeOffset? StartDate { get; set; }
```

- [ ] **Step 4: Update UpdateTrackedTaskDto**

In `Timinute/Shared/Dtos/TrackedTask/UpdateTrackedTaskDto.cs`, change:
```csharp
public DateTime? StartDate { get; set; }
public DateTime? EndDate { get; set; }
```
To:
```csharp
public DateTimeOffset? StartDate { get; set; }
public DateTimeOffset? EndDate { get; set; }
```

- [ ] **Step 5: Update ProjectDataItemsPerMonthDto**

In `Timinute/Shared/Dtos/Dashboard/ProjectDataItemsPerMonthDto.cs`, change:
```csharp
public DateTime Time { get; set; }
```
To:
```csharp
public DateTimeOffset Time { get; set; }
```

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server/Models/TrackedTask.cs Timinute/Shared/Dtos/
git commit -m "refactor: change DateTime to DateTimeOffset in TrackedTask model and DTOs"
```

---

### Task 2: Update controllers

**Files:**
- Modify: `Timinute/Server/Controllers/TrackedTaskController.cs`
- Modify: `Timinute/Server/Controllers/AnalyticsController.cs`
- Modify: `Timinute/Server/Controllers/ExportController.cs`

- [ ] **Step 1: Update TrackedTaskController.CreateTrackedTask**

Replace lines 94-98:
```csharp
var newTrackedTask = mapper.Map<TrackedTask>(trackedTask);
newTrackedTask.UserId = userId;
var utcStart = trackedTask.StartDate!.Value.ToUniversalTime();
newTrackedTask.StartDate = utcStart;
newTrackedTask.EndDate = utcStart + newTrackedTask.Duration;
```
With:
```csharp
var newTrackedTask = mapper.Map<TrackedTask>(trackedTask);
newTrackedTask.UserId = userId;
newTrackedTask.StartDate = trackedTask.StartDate!.Value.ToUniversalTime();
newTrackedTask.EndDate = newTrackedTask.StartDate + newTrackedTask.Duration;
```

- [ ] **Step 2: Update TrackedTaskController.UpdateTrackedTask**

Replace lines 158-169:
```csharp
var updatedTrackedTask = mapper.Map(trackedTask, foundTrackedTask);
updatedTrackedTask.StartDate = updatedTrackedTask.StartDate.ToUniversalTime();

if (updatedTrackedTask.EndDate.HasValue)
{
    updatedTrackedTask.EndDate = updatedTrackedTask.EndDate.Value.ToUniversalTime();

    if (updatedTrackedTask.EndDate.Value <= updatedTrackedTask.StartDate)
    {
        return BadRequest("End date must be strictly after start date.");
    }

    updatedTrackedTask.Duration = updatedTrackedTask.EndDate.Value - updatedTrackedTask.StartDate;
}
```
With:
```csharp
var updatedTrackedTask = mapper.Map(trackedTask, foundTrackedTask);
updatedTrackedTask.StartDate = updatedTrackedTask.StartDate.ToUniversalTime();

if (updatedTrackedTask.EndDate.HasValue)
{
    updatedTrackedTask.EndDate = updatedTrackedTask.EndDate.Value.ToUniversalTime();

    if (updatedTrackedTask.EndDate.Value <= updatedTrackedTask.StartDate)
    {
        return BadRequest("End date must be strictly after start date.");
    }

    updatedTrackedTask.Duration = updatedTrackedTask.EndDate.Value - updatedTrackedTask.StartDate;
}
```

Note: The code looks the same but now operates on `DateTimeOffset`. `DateTimeOffset.ToUniversalTime()` is always correct regardless of input — no behavior change needed.

- [ ] **Step 3: Update AnalyticsController.GetAmountWorkTimeByMonth**

Replace line 169:
```csharp
var month = new DateTime(amountWorkTimeByMonthDto.Year, amountWorkTimeByMonthDto.Month, 1).ToUniversalTime();
```
With:
```csharp
var month = new DateTimeOffset(amountWorkTimeByMonthDto.Year, amountWorkTimeByMonthDto.Month, 1, 0, 0, 0, TimeSpan.Zero);
```

- [ ] **Step 4: Update AnalyticsController.GetProjectWorkTimePerMonths**

Replace line 104:
```csharp
projectDataItemsPerMonth.Time = new DateTime(projectTimeByMonth.Key.Year, projectTimeByMonth.Key.Month, 1);
```
With:
```csharp
projectDataItemsPerMonth.Time = new DateTimeOffset(projectTimeByMonth.Key.Year, projectTimeByMonth.Key.Month, 1, 0, 0, 0, TimeSpan.Zero);
```

- [ ] **Step 5: Update ExportController query parameters**

In `ExportController.cs`, change all three endpoint signatures from `DateTime?` to `DateTimeOffset?` for `from` and `to` parameters:

ExportTasks (line 32-33):
```csharp
[FromQuery] DateTimeOffset? from = null,
[FromQuery] DateTimeOffset? to = null,
```

ExportProjects (line 68-69):
```csharp
[FromQuery] DateTimeOffset? from = null,
[FromQuery] DateTimeOffset? to = null,
```

ExportAnalytics (line 104-105):
```csharp
[FromQuery] DateTimeOffset? from = null,
[FromQuery] DateTimeOffset? to = null,
```

And update the filter comparisons — replace `.ToUniversalTime()` calls. For example in ExportTasks:
```csharp
&& (from == null || t.StartDate >= from.Value.ToUniversalTime())
&& (to == null || t.StartDate <= to.Value.ToUniversalTime())
```
This now works correctly because `DateTimeOffset.ToUniversalTime()` is always deterministic.

- [ ] **Step 6: Update ExportController date formatting**

In ExportTasks DTO mapping (line 54-57), change `.ToString()` calls:
```csharp
StartDate = t.StartDate.ToString("yyyy-MM-dd HH:mm"),
EndDate = t.EndDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
Date = t.StartDate.ToString("yyyy-MM-dd"),
```
To (use UtcDateTime for consistent formatting):
```csharp
StartDate = t.StartDate.UtcDateTime.ToString("yyyy-MM-dd HH:mm"),
EndDate = t.EndDate?.UtcDateTime.ToString("yyyy-MM-dd HH:mm") ?? "",
Date = t.StartDate.UtcDateTime.ToString("yyyy-MM-dd"),
```

- [ ] **Step 7: Verify build compiles**

```bash
dotnet build Timinute.sln
```
Expected: May have errors in tests (will fix in Task 3). Server project should compile.

- [ ] **Step 8: Commit**

```bash
git add Timinute/Server/Controllers/
git commit -m "refactor: update controllers for DateTimeOffset"
```

---

### Task 3: Update seed data and tests

**Files:**
- Modify: `Timinute/Server/Data/ApplicationDbContext.cs`
- Modify: `Timinute/Server.Tests/Helpers/TestHelper.cs`
- Modify: `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs`
- Modify: `Timinute/Server.Tests/Controllers/AnalyticsControllerTest.cs`
- Modify: `Timinute/Server.Tests/Controllers/ExportControllerTest.cs`
- Modify: `Timinute/Server.Tests/Repositories/TrackedTaskRepositoryTest.cs`

- [ ] **Step 1: Update ApplicationDbContext seed data**

In `ApplicationDbContext.cs` `FillDataToDB` method, replace all `new DateTime(y, m, d, h, m, s)` with `new DateTimeOffset(y, m, d, h, m, s, TimeSpan.Zero)`. For example:

```csharp
StartDate = new DateTimeOffset(2022, 1, 1, 9, 0, 0, TimeSpan.Zero), EndDate = new DateTimeOffset(2022, 1, 1, 11, 0, 0, TimeSpan.Zero)
```

Apply to all 7 TrackedTask entries in the seed data.

- [ ] **Step 2: Update TestHelper.FillInitData**

Replace all `new DateTime(2021, 10, 1, 8, 0, 0)` with `new DateTimeOffset(2021, 10, 1, 8, 0, 0, TimeSpan.Zero)` and similar for EndDate values. Apply to all 8 TrackedTask entries.

- [ ] **Step 3: Update TestHelper.FillAnalyticsData**

Replace lines 101-103:
```csharp
var now = DateTime.UtcNow;
var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
var first = month.AddMonths(-1);
```
With:
```csharp
var now = DateTimeOffset.UtcNow;
var month = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
var first = month.AddMonths(-1);
```

Update the 11 TrackedTask entries — `StartDate = first` and `EndDate = first.AddHours(N)` will work as-is since `first` is now `DateTimeOffset`.

- [ ] **Step 4: Update TrackedTaskControllerTest**

Replace all `DateTime.Now` with `DateTimeOffset.UtcNow` and `DateTime startDate` with `DateTimeOffset startDate` in these tests:
- `Update_Existing_TrackedTask_Test` (line 196)
- `Update_Not_Existing_TrackedTask_Test` (line 228)
- `Create_New_TrackedTask_Test` (line 255)

Also update the assertion in `Update_Existing_TrackedTask_Test` (line 220):
```csharp
Assert.Equal(trackedTaskToUpdate.StartDate, updatedTrackedTask.StartDate.ToLocalTime());
```
To:
```csharp
Assert.Equal(trackedTaskToUpdate.StartDate!.Value.UtcDateTime, updatedTrackedTask.StartDate.UtcDateTime, TimeSpan.FromSeconds(1));
```

Update `Update_TrackedTask_Another_User_Test` (line 292) — change `DateTime.UtcNow` to `DateTimeOffset.UtcNow`.

Update `Update_TrackedTask_EndDate_Before_StartDate_Returns_BadRequest_Test` — change `new DateTime(...)` to `new DateTimeOffset(..., TimeSpan.Zero)`.

- [ ] **Step 5: Update AnalyticsControllerTest**

Replace `DateTime.UtcNow` with `DateTimeOffset.UtcNow` in:
- `Get_Amount_Work_Time_Last_Month_Test` (line 41)
- `Get_Amount_Work_Time_This_Month_Test` (line 68)
- `Get_Work_Time_Per_Months_Test` (line 108)

Replace `DateTime.Today.ToUniversalTime()` with `DateTimeOffset.UtcNow` in `Get_Work_Time_Per_Months` (line 139-140).

- [ ] **Step 6: Update ExportControllerTest**

Replace `new System.DateTime(2021, 10, 1)` with `new DateTimeOffset(2021, 10, 1, 0, 0, 0, TimeSpan.Zero)` in `Export_Tasks_With_DateRange_Filters` (line 68-69).

- [ ] **Step 7: Update TrackedTaskRepositoryTest**

Replace `DateTime.UtcNow` with `DateTimeOffset.UtcNow` in:
- `Add_TrackedTask_Test` (line 102)
- `Add_TrackedTask_Without_User_Test` (line 127)

Also update the `new TrackedTask { ... StartDate = dateNow, EndDate = dateNow.AddHours(3) ... }` — works as-is since `DateTimeOffset` has `AddHours`.

- [ ] **Step 8: Build and run all tests**

```bash
dotnet build Timinute.sln && dotnet test
```
Expected: 0 errors, 60/60 tests pass

- [ ] **Step 9: Commit**

```bash
git add Timinute/Server/Data/ApplicationDbContext.cs Timinute/Server.Tests/
git commit -m "refactor: update seed data and tests for DateTimeOffset"
```

---

### Task 4: Update client model and generate EF migration

**Files:**
- Modify: `Timinute/Client/Models/TrackedTask.cs`
- New EF migration

- [ ] **Step 1: Update Client TrackedTask model constructor**

In `Timinute/Client/Models/TrackedTask.cs`, update the constructor (lines 38-39):
```csharp
StartDate = trackedTask.StartDate.ToLocalTime();
EndDate = trackedTask.EndDate?.ToLocalTime();
```
To:
```csharp
StartDate = trackedTask.StartDate.LocalDateTime;
EndDate = trackedTask.EndDate?.LocalDateTime;
```

The client model properties stay `DateTime` for RadzenDatePicker compatibility.

- [ ] **Step 2: Generate EF migration**

```bash
dotnet ef migrations add MigrateDateTimeToDateTimeOffset --project Timinute/Server/Timinute.Server.csproj
```

This generates an `ALTER COLUMN` from `datetime2` to `datetimeoffset`. SQL Server treats existing values as UTC (+00:00).

- [ ] **Step 3: Verify build and tests**

```bash
dotnet build Timinute.sln && dotnet test
```
Expected: 0 errors, 60/60 tests pass

- [ ] **Step 4: Commit**

```bash
git add Timinute/Client/Models/TrackedTask.cs Timinute/Server/Data/Migrations/
git commit -m "refactor: update client model and add EF migration for DateTimeOffset"
```

---

### Task 5: Final verification

- [ ] **Step 1: Clean build**

```bash
dotnet clean Timinute.sln && dotnet build Timinute.sln
```
Expected: 0 errors

- [ ] **Step 2: Run full test suite**

```bash
dotnet test --verbosity normal
```
Expected: 60/60 pass

- [ ] **Step 3: Verify no DateTime.Now or ToUniversalTime on Unspecified remains**

Search for risky patterns:
```bash
grep -rn "DateTime\.Now\b" Timinute/Server/ Timinute/Server.Tests/ --include="*.cs"
grep -rn "\.ToUniversalTime()" Timinute/Server/Controllers/ --include="*.cs"
```
Expected: `DateTime.Now` only in client code. `ToUniversalTime()` calls should only be on `DateTimeOffset` (which is always safe).

- [ ] **Step 4: Verify git status**

```bash
git status
```
Expected: Clean working tree
