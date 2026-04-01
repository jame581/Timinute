# P0: Data Validation & Authorization Fixes

## Goal

Fix authorization gaps and add input validation to all DTOs to prevent invalid data and unauthorized access.

## Scope

- Authorization ownership checks on GET single-entity endpoints
- Data Annotation validation attributes on all create/update DTOs
- Custom TimeSpan validation attribute for Duration > 0
- Controller-level EndDate >= StartDate validation
- Tests for all new behavior

Out of scope: soft delete/archiving, FluentValidation, new features.

## Authorization Fixes

Two GET endpoints return data without checking user ownership:

**ProjectController.GetProject(id):** After finding the project, return NotFound if `project.UserId != userId`. Use NotFound (not Unauthorized) to avoid revealing resource existence.

**TrackedTaskController.GetTrackedTask(id):** Same pattern — return NotFound if `trackedTask.UserId != userId`.

## DTO Validation Attributes

### CreateProjectDto
- `Name`: `[Required]`, `[StringLength(100, MinimumLength = 2)]`

### UpdateProjectDto
- `ProjectId`: `[Required]`
- `Name`: `[Required]`, `[StringLength(100, MinimumLength = 2)]`

### CreateTrackedTaskDto (existing attributes kept, additions noted)
- `Name`: `[Required]`, `[StringLength(50, MinimumLength = 2)]` (existing)
- `StartDate`: `[Required]` (existing)
- `Duration`: `[Required]`, `[MinDuration]` (new — must be > TimeSpan.Zero)

### UpdateTrackedTaskDto
- `TaskId`: `[Required]`
- `Name`: `[Required]`, `[StringLength(50, MinimumLength = 2)]`
- `StartDate`: `[Required]`

No Duration validation on update — controller recalculates from EndDate - StartDate.

## Custom MinDuration Attribute

File: `Timinute/Shared/Validators/MinDurationAttribute.cs`

A `ValidationAttribute` subclass that checks `TimeSpan > TimeSpan.Zero`. Placed in Shared project so both client and server can reference it. Applied only to `CreateTrackedTaskDto.Duration`.

## Controller EndDate Validation

In `TrackedTaskController.UpdateTrackedTask`: after mapping the updated StartDate/EndDate, check that EndDate >= StartDate. Return `BadRequest("End date must be after start date.")` if invalid.

Only needed on update — CreateTrackedTask calculates EndDate from StartDate + Duration, so it's always valid by construction.

## Validation Rules Summary

| Field | Create | Update |
|-------|--------|--------|
| Project.Name | Required, 2-100 chars | Required, 2-100 chars |
| Project.ProjectId | N/A | Required |
| Task.Name | Required, 2-50 chars | Required, 2-50 chars |
| Task.TaskId | N/A | Required |
| Task.StartDate | Required | Required |
| Task.Duration | Required, > 0 | N/A (recalculated) |
| Task.EndDate | N/A (calculated) | Optional, must be >= StartDate |

## Test Plan

**Authorization:**
- `Get_Project_Another_User_Test` — GetProject returns NotFound for non-owner
- `Get_TrackedTask_Another_User_Test` — GetTrackedTask returns NotFound for non-owner

**Validation:**
- `Create_Project_Empty_Name_Test` — 422 for empty/null name
- `Update_TrackedTask_EndDate_Before_StartDate_Test` — BadRequest for EndDate < StartDate

Tests use existing patterns: `ControllerTestBase<T>`, `TestHelper` seed data, same AutoMapper setup.

## Files to Modify

- `Timinute/Shared/Dtos/Project/CreateProjectDto.cs`
- `Timinute/Shared/Dtos/Project/UpdateProjectDto.cs`
- `Timinute/Shared/Dtos/TrackedTask/UpdateTrackedTaskDto.cs`
- `Timinute/Shared/Dtos/TrackedTask/CreateTrackedTaskDto.cs` (add Duration validation)
- `Timinute/Server/Controllers/ProjectController.cs` (ownership check on GetProject)
- `Timinute/Server/Controllers/TrackedTaskController.cs` (ownership check on GetTrackedTask + EndDate validation)
- `Timinute/Server.Tests/Controllers/ProjectControllerTest.cs`
- `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs`

## Files to Create

- `Timinute/Shared/Validators/MinDurationAttribute.cs`
