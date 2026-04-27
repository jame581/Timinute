# Data Export Feature Design

## Goal

Add CSV and Excel (.xlsx) export for tracked tasks, project summaries, and monthly analytics via server-side API endpoints.

## Scope

- Three new API endpoints on an `ExportController`
- CSV generation via CsvHelper library
- Excel generation via ClosedXML library
- Optional filtering: date range, project, search term
- Export DTOs for flat/formatted output
- Unit tests for services and controller

Out of scope: PDF export, client-side UI buttons (API only for now), scheduled/email reports.

## New Dependencies

- `CsvHelper` (latest stable) — CSV serialization
- `ClosedXML` (latest stable, MIT license) — Excel generation

Both added to `Timinute/Server/Timinute.Server.csproj`.

## API Endpoints

All on `ExportController`, `[Authorize]`, route prefix `[controller]`.

### GET /export/tasks

Query params: `format` (csv|xlsx), `from` (DateTime?), `to` (DateTime?), `projectId` (string?), `search` (string?)

Returns tracked tasks for the current user.

Columns: Name, ProjectName, StartDate, EndDate, Duration (HH:mm:ss), Date (yyyy-MM-dd)

Sorted by StartDate descending. Includes Project navigation property for ProjectName.

### GET /export/projects

Query params: `format` (csv|xlsx), `from` (DateTime?), `to` (DateTime?), `search` (string?)

Returns per-project summary for the current user.

Columns: ProjectName, TotalHours (HH:mm:ss), TaskCount

Aggregates tracked task durations grouped by project. Filters apply to the underlying tasks (date range limits which tasks count toward totals).

### GET /export/analytics

Query params: `format` (csv|xlsx), `from` (DateTime?), `to` (DateTime?)

Returns monthly work time analytics for the current user.

Columns: Month (yyyy MMM), TotalHours (HH:mm:ss), TopProject, TopProjectHours (HH:mm:ss)

One row per month. Filters limit which months are included.

### Response

All endpoints return `FileContentResult` with:
- Content type: `text/csv` or `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- `Content-Disposition: attachment; filename="<type>-export-<yyyy-MM-dd>.<ext>"`
- Example: `tracked-tasks-export-2026-04-01.csv`

Invalid or missing `format` defaults to CSV.

## Export Services

### IExportService

```csharp
public interface IExportService
{
    byte[] ToCsv<T>(IEnumerable<T> data);
    byte[] ToExcel<T>(IEnumerable<T> data, string sheetName);
}
```

### ExportService

Single implementation using CsvHelper for CSV and ClosedXML for Excel. Registered as singleton in DI (stateless, thread-safe). CSV injection protection is applied at the controller level via `SanitizeForExport()` which prefixes formula-triggering characters (`=`, `+`, `-`, `@`) with a single quote on all user-controlled string fields during DTO mapping.

- `ToCsv<T>`: Uses CsvHelper to write headers + rows to a MemoryStream, returns bytes.
- `ToExcel<T>`: Creates XLWorkbook with one worksheet, inserts headers from property names, fills rows, returns bytes.

## Export DTOs

Server-only DTOs in `Timinute/Server/Models/Export/`:

### TaskExportDto
- `Name` (string)
- `ProjectName` (string)
- `StartDate` (string, formatted yyyy-MM-dd HH:mm)
- `EndDate` (string, formatted yyyy-MM-dd HH:mm)
- `Duration` (string, formatted HH:mm:ss)
- `Date` (string, formatted yyyy-MM-dd)

### ProjectExportDto
- `ProjectName` (string)
- `TotalHours` (string, formatted HH:mm:ss)
- `TaskCount` (int)

### AnalyticsExportDto
- `Month` (string, formatted yyyy MMM)
- `TotalHours` (string, formatted HH:mm:ss)
- `TopProject` (string)
- `TopProjectHours` (string, formatted HH:mm:ss)

## Data Flow

1. Client calls `GET /export/tasks?format=xlsx&from=2026-03-01&to=2026-03-31`
2. Controller extracts userId from JWT claims
3. Controller queries repository with filters (date range, projectId, search)
4. Controller maps entity results to export DTOs with formatted strings
5. Controller passes DTOs to `IExportService.ToExcel()` or `ToCsv()`
6. Controller returns `FileContentResult` with bytes and appropriate headers

## Files to Create

- `Timinute/Server/Controllers/ExportController.cs`
- `Timinute/Server/Services/IExportService.cs`
- `Timinute/Server/Services/ExportService.cs`
- `Timinute/Server/Models/Export/TaskExportDto.cs`
- `Timinute/Server/Models/Export/ProjectExportDto.cs`
- `Timinute/Server/Models/Export/AnalyticsExportDto.cs`

## Files to Modify

- `Timinute/Server/Timinute.Server.csproj` (add CsvHelper, ClosedXML)
- `Timinute/Server/Program.cs` (register IExportService)

## Test Plan

### Export Service Tests
- `CsvExportService_Returns_Valid_Csv` — verify headers and row count
- `ExcelExportService_Returns_Valid_Xlsx` — verify parseable Excel with correct sheet and row count

### Controller Tests
- `Export_Tasks_Csv_Returns_File` — verify FileContentResult with text/csv
- `Export_Tasks_Xlsx_Returns_File` — verify FileContentResult with Excel content type
- `Export_Tasks_With_DateRange_Filters` — verify only tasks within range exported
- `Export_Tasks_Another_User_Returns_Empty` — verify user isolation
