# Data Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add CSV and Excel export endpoints for tracked tasks, project summaries, and monthly analytics.

**Architecture:** New `ExportController` with three GET endpoints returning `FileContentResult`. An `IExportService` handles CSV (CsvHelper) and Excel (ClosedXML) generation. Export DTOs provide flat, formatted data. Repository queries with optional filters feed the export pipeline.

**Tech Stack:** .NET 10, CsvHelper, ClosedXML, xUnit, Moq, EF Core InMemory

---

### Task 1: Add NuGet packages

**Files:**
- Modify: `Timinute/Server/Timinute.Server.csproj`

- [ ] **Step 1: Add CsvHelper and ClosedXML packages**

```bash
cd Timinute/Server
dotnet add package CsvHelper
dotnet add package ClosedXML
```

- [ ] **Step 2: Verify build**

```bash
dotnet build Timinute.sln
```
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add Timinute/Server/Timinute.Server.csproj
git commit -m "chore: add CsvHelper and ClosedXML packages for data export"
```

---

### Task 2: Create export DTOs

**Files:**
- Create: `Timinute/Server/Models/Export/TaskExportDto.cs`
- Create: `Timinute/Server/Models/Export/ProjectExportDto.cs`
- Create: `Timinute/Server/Models/Export/AnalyticsExportDto.cs`

- [ ] **Step 1: Create TaskExportDto**

```csharp
namespace Timinute.Server.Models.Export
{
    public class TaskExportDto
    {
        public string Name { get; set; } = null!;
        public string ProjectName { get; set; } = null!;
        public string StartDate { get; set; } = null!;
        public string EndDate { get; set; } = null!;
        public string Duration { get; set; } = null!;
        public string Date { get; set; } = null!;
    }
}
```

- [ ] **Step 2: Create ProjectExportDto**

```csharp
namespace Timinute.Server.Models.Export
{
    public class ProjectExportDto
    {
        public string ProjectName { get; set; } = null!;
        public string TotalHours { get; set; } = null!;
        public int TaskCount { get; set; }
    }
}
```

- [ ] **Step 3: Create AnalyticsExportDto**

```csharp
namespace Timinute.Server.Models.Export
{
    public class AnalyticsExportDto
    {
        public string Month { get; set; } = null!;
        public string TotalHours { get; set; } = null!;
        public string TopProject { get; set; } = null!;
        public string TopProjectHours { get; set; } = null!;
    }
}
```

- [ ] **Step 4: Verify build**

```bash
dotnet build Timinute.sln
```

- [ ] **Step 5: Commit**

```bash
git add Timinute/Server/Models/Export/
git commit -m "feat: add export DTOs for tasks, projects, and analytics"
```

---

### Task 3: Create ExportService with CSV support

**Files:**
- Create: `Timinute/Server/Services/IExportService.cs`
- Create: `Timinute/Server/Services/ExportService.cs`
- Create: `Timinute/Server.Tests/Services/ExportServiceTest.cs`

- [ ] **Step 1: Create IExportService interface**

```csharp
namespace Timinute.Server.Services
{
    public interface IExportService
    {
        byte[] ToCsv<T>(IEnumerable<T> data);
        byte[] ToExcel<T>(IEnumerable<T> data, string sheetName);
    }
}
```

- [ ] **Step 2: Write failing CSV test**

```csharp
using Timinute.Server.Models.Export;
using Timinute.Server.Services;
using Xunit;

namespace Timinute.Server.Tests.Services
{
    public class ExportServiceTest
    {
        private readonly ExportService _exportService;

        public ExportServiceTest()
        {
            _exportService = new ExportService();
        }

        [Fact]
        public void ToCsv_Returns_Valid_Csv_With_Headers_And_Rows()
        {
            var data = new List<TaskExportDto>
            {
                new TaskExportDto { Name = "Task 1", ProjectName = "Project A", StartDate = "2026-04-01 09:00", EndDate = "2026-04-01 11:00", Duration = "02:00:00", Date = "2026-04-01" },
                new TaskExportDto { Name = "Task 2", ProjectName = "Project B", StartDate = "2026-04-01 13:00", EndDate = "2026-04-01 15:00", Duration = "02:00:00", Date = "2026-04-01" },
            };

            var result = _exportService.ToCsv(data);

            Assert.NotNull(result);
            Assert.True(result.Length > 0);

            var csv = System.Text.Encoding.UTF8.GetString(result);
            var lines = csv.Trim().Split('\n');

            Assert.Equal(3, lines.Length); // header + 2 rows
            Assert.Contains("Name", lines[0]);
            Assert.Contains("ProjectName", lines[0]);
            Assert.Contains("Task 1", lines[1]);
            Assert.Contains("Task 2", lines[2]);
        }

        [Fact]
        public void ToCsv_Empty_Data_Returns_Headers_Only()
        {
            var data = new List<TaskExportDto>();

            var result = _exportService.ToCsv(data);

            Assert.NotNull(result);
            var csv = System.Text.Encoding.UTF8.GetString(result);
            var lines = csv.Trim().Split('\n');

            Assert.Single(lines); // header only
            Assert.Contains("Name", lines[0]);
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

```bash
dotnet test --filter "FullyQualifiedName~ExportServiceTest.ToCsv_Returns_Valid_Csv"
```
Expected: FAIL (ExportService doesn't exist yet)

- [ ] **Step 4: Implement ExportService with CSV**

```csharp
using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;

namespace Timinute.Server.Services
{
    public class ExportService : IExportService
    {
        public byte[] ToCsv<T>(IEnumerable<T> data)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, System.Text.Encoding.UTF8);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            csv.WriteRecords(data);
            writer.Flush();

            return memoryStream.ToArray();
        }

        public byte[] ToExcel<T>(IEnumerable<T> data, string sheetName)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);
            worksheet.Cell(1, 1).InsertTable(data);

            using var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);

            return memoryStream.ToArray();
        }
    }
}
```

- [ ] **Step 5: Run CSV tests to verify they pass**

```bash
dotnet test --filter "FullyQualifiedName~ExportServiceTest.ToCsv"
```
Expected: PASS (both tests)

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server/Services/ Timinute/Server.Tests/Services/
git commit -m "feat: add ExportService with CSV support"
```

---

### Task 4: Add Excel tests to ExportService

**Files:**
- Modify: `Timinute/Server.Tests/Services/ExportServiceTest.cs`

- [ ] **Step 1: Write Excel tests**

Add to `ExportServiceTest.cs`:

```csharp
[Fact]
public void ToExcel_Returns_Valid_Xlsx_With_Data()
{
    var data = new List<TaskExportDto>
    {
        new TaskExportDto { Name = "Task 1", ProjectName = "Project A", StartDate = "2026-04-01 09:00", EndDate = "2026-04-01 11:00", Duration = "02:00:00", Date = "2026-04-01" },
        new TaskExportDto { Name = "Task 2", ProjectName = "Project B", StartDate = "2026-04-01 13:00", EndDate = "2026-04-01 15:00", Duration = "02:00:00", Date = "2026-04-01" },
    };

    var result = _exportService.ToExcel(data, "Tasks");

    Assert.NotNull(result);
    Assert.True(result.Length > 0);

    // Verify it's a valid Excel file by reading it back
    using var stream = new MemoryStream(result);
    using var workbook = new XLWorkbook(stream);

    Assert.Single(workbook.Worksheets);
    Assert.Equal("Tasks", workbook.Worksheets.First().Name);

    var worksheet = workbook.Worksheets.First();
    // InsertTable creates header row + data rows
    Assert.Equal(3, worksheet.RowsUsed().Count()); // header + 2 rows
}

[Fact]
public void ToExcel_Empty_Data_Returns_Valid_Xlsx()
{
    var data = new List<TaskExportDto>();

    var result = _exportService.ToExcel(data, "Empty");

    Assert.NotNull(result);
    Assert.True(result.Length > 0);

    using var stream = new MemoryStream(result);
    using var workbook = new XLWorkbook(stream);

    Assert.Equal("Empty", workbook.Worksheets.First().Name);
}
```

Add at top of file:
```csharp
using ClosedXML.Excel;
```

- [ ] **Step 2: Run Excel tests**

```bash
dotnet test --filter "FullyQualifiedName~ExportServiceTest.ToExcel"
```
Expected: PASS

- [ ] **Step 3: Run all export tests**

```bash
dotnet test --filter "FullyQualifiedName~ExportServiceTest"
```
Expected: 4/4 pass

- [ ] **Step 4: Commit**

```bash
git add Timinute/Server.Tests/Services/ExportServiceTest.cs
git commit -m "test: add Excel export tests"
```

---

### Task 5: Create ExportController with tasks endpoint

**Files:**
- Create: `Timinute/Server/Controllers/ExportController.cs`
- Modify: `Timinute/Server/Program.cs` (register IExportService)
- Create: `Timinute/Server.Tests/Controllers/ExportControllerTest.cs`

- [ ] **Step 1: Register ExportService in DI**

In `Timinute/Server/Program.cs`, in the `DependecyInjection()` method, add:

```csharp
builder.Services.AddTransient<IExportService, ExportService>();
```

Add the using at the function scope or file level:
```csharp
using Timinute.Server.Services;
```

- [ ] **Step 2: Write failing test for task export**

Create `Timinute/Server.Tests/Controllers/ExportControllerTest.cs`:

```csharp
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading.Tasks;
using Timinute.Server.Controllers;
using Timinute.Server.Data;
using Timinute.Server.Repository;
using Timinute.Server.Services;
using Timinute.Server.Tests.Helpers;
using System.Security.Claims;
using Xunit;

namespace Timinute.Server.Tests.Controllers
{
    public class ExportControllerTest : ControllerTestBase<ExportController>
    {
        private readonly IMapper _mapper;
        private readonly Mock<ILogger<ExportController>> _loggerMock;
        private readonly IExportService _exportService;

        private const string _databaseName = "ExportController_Test_DB";

        public ExportControllerTest()
        {
            var configExpression = new MapperConfigurationExpression();
            configExpression.AddProfile<MappingProfile>();
            var configuration = new MapperConfiguration(configExpression, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            _mapper = new Mapper(configuration);

            _loggerMock = new Mock<ILogger<ExportController>>();
            _exportService = new ExportService();
        }

        [Fact]
        public async Task Export_Tasks_Csv_Returns_File()
        {
            ExportController controller = await CreateController();

            var actionResult = await controller.ExportTasks("csv", null, null, null, null);

            Assert.NotNull(actionResult);
            Assert.IsType<FileContentResult>(actionResult);

            var fileResult = actionResult as FileContentResult;
            Assert.NotNull(fileResult);
            Assert.Equal("text/csv", fileResult!.ContentType);
            Assert.Contains("tracked-tasks-export-", fileResult.FileDownloadName);
            Assert.EndsWith(".csv", fileResult.FileDownloadName);
            Assert.True(fileResult.FileContents.Length > 0);
        }

        [Fact]
        public async Task Export_Tasks_Xlsx_Returns_File()
        {
            ExportController controller = await CreateController();

            var actionResult = await controller.ExportTasks("xlsx", null, null, null, null);

            Assert.NotNull(actionResult);
            Assert.IsType<FileContentResult>(actionResult);

            var fileResult = actionResult as FileContentResult;
            Assert.NotNull(fileResult);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult!.ContentType);
            Assert.EndsWith(".xlsx", fileResult!.FileDownloadName);
        }

        [Fact]
        public async Task Export_Tasks_With_DateRange_Filters()
        {
            ExportController controller = await CreateController();

            // Seed data has tasks on 2021-10-01. Filter to include them.
            var from = new DateTime(2021, 10, 1);
            var to = new DateTime(2021, 10, 31);

            var actionResult = await controller.ExportTasks("csv", from, to, null, null);

            var fileResult = actionResult as FileContentResult;
            Assert.NotNull(fileResult);

            var csv = System.Text.Encoding.UTF8.GetString(fileResult!.FileContents);
            var lines = csv.Trim().Split('\n');

            // ApplicationUser1 has 4 tasks in seed data, all on 2021-10-01
            Assert.Equal(5, lines.Length); // header + 4 data rows
        }

        [Fact]
        public async Task Export_Tasks_Another_User_Returns_Empty_File()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "AuthTest");
            ExportController controller = await CreateController(applicationDbContext, "NonExistentUser");

            var actionResult = await controller.ExportTasks("csv", null, null, null, null);

            var fileResult = actionResult as FileContentResult;
            Assert.NotNull(fileResult);

            var csv = System.Text.Encoding.UTF8.GetString(fileResult!.FileContents);
            var lines = csv.Trim().Split('\n');

            Assert.Single(lines); // header only, no data
        }

        protected override async Task<ExportController> CreateController(ApplicationDbContext? applicationDbContext = null, string userId = "ApplicationUser1")
        {
            if (applicationDbContext == null)
            {
                applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName);
            }

            var repositoryFactory = new RepositoryFactory(applicationDbContext);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                new Claim("sub", userId),
                new Claim(ClaimTypes.Name, "test1@email.com")
            }));

            ExportController controller = new(repositoryFactory, _exportService, _loggerMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                }
            };

            return controller;
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet test --filter "FullyQualifiedName~ExportControllerTest"
```
Expected: FAIL (ExportController doesn't exist)

- [ ] **Step 4: Implement ExportController with tasks endpoint**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Models.Export;
using Timinute.Server.Repository;
using Timinute.Server.Services;

namespace Timinute.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class ExportController : ControllerBase
    {
        private readonly IRepository<TrackedTask> taskRepository;
        private readonly IRepository<Project> projectRepository;
        private readonly IExportService exportService;
        private readonly ILogger<ExportController> logger;

        public ExportController(IRepositoryFactory repositoryFactory, IExportService exportService, ILogger<ExportController> logger)
        {
            this.exportService = exportService;
            this.logger = logger;

            taskRepository = repositoryFactory.GetRepository<TrackedTask>();
            projectRepository = repositoryFactory.GetRepository<Project>();
        }

        [HttpGet("tasks")]
        public async Task<ActionResult> ExportTasks(
            [FromQuery] string? format = "csv",
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? projectId = null,
            [FromQuery] string? search = null)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var tasks = await taskRepository.Get(
                t => t.UserId == userId
                    && (from == null || t.StartDate >= from.Value.ToUniversalTime())
                    && (to == null || t.StartDate <= to.Value.ToUniversalTime())
                    && (projectId == null || t.ProjectId == projectId)
                    && (search == null || t.Name.Contains(search)),
                includeProperties: nameof(Project));

            var exportData = tasks.Select(t => new TaskExportDto
            {
                Name = t.Name,
                ProjectName = t.Project?.Name ?? "None",
                StartDate = t.StartDate.ToString("yyyy-MM-dd HH:mm"),
                EndDate = t.EndDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
                Duration = FormatDuration(t.Duration),
                Date = t.StartDate.ToString("yyyy-MM-dd"),
            }).ToList();

            return GenerateFile(exportData, format, "tracked-tasks");
        }

        [HttpGet("projects")]
        public async Task<ActionResult> ExportProjects(
            [FromQuery] string? format = "csv",
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? search = null)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var tasks = await taskRepository.Get(
                t => t.UserId == userId
                    && (from == null || t.StartDate >= from.Value.ToUniversalTime())
                    && (to == null || t.StartDate <= to.Value.ToUniversalTime()),
                includeProperties: nameof(Project));

            var exportData = tasks
                .GroupBy(t => t.ProjectId ?? "None")
                .Select(g =>
                {
                    var projectName = g.First().Project?.Name ?? "None";
                    if (!string.IsNullOrEmpty(search) && !projectName.Contains(search, StringComparison.OrdinalIgnoreCase))
                        return null;

                    return new ProjectExportDto
                    {
                        ProjectName = projectName,
                        TotalHours = FormatDuration(TimeSpan.FromSeconds(g.Sum(t => t.Duration.TotalSeconds))),
                        TaskCount = g.Count(),
                    };
                })
                .Where(x => x != null)
                .ToList();

            return GenerateFile(exportData!, format, "projects");
        }

        [HttpGet("analytics")]
        public async Task<ActionResult> ExportAnalytics(
            [FromQuery] string? format = "csv",
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var tasks = await taskRepository.Get(
                t => t.UserId == userId
                    && (from == null || t.StartDate >= from.Value.ToUniversalTime())
                    && (to == null || t.StartDate <= to.Value.ToUniversalTime()),
                includeProperties: nameof(Project));

            var exportData = tasks
                .GroupBy(t => new { t.StartDate.Year, t.StartDate.Month })
                .Select(g =>
                {
                    var topProject = g
                        .GroupBy(t => t.ProjectId)
                        .OrderByDescending(pg => pg.Sum(t => t.Duration.TotalSeconds))
                        .First();

                    var topProjectName = topProject.First().Project?.Name ?? "None";
                    var topProjectSeconds = topProject.Sum(t => t.Duration.TotalSeconds);

                    return new AnalyticsExportDto
                    {
                        Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("yyyy MMM"),
                        TotalHours = FormatDuration(TimeSpan.FromSeconds(g.Sum(t => t.Duration.TotalSeconds))),
                        TopProject = topProjectName,
                        TopProjectHours = FormatDuration(TimeSpan.FromSeconds(topProjectSeconds)),
                    };
                })
                .ToList();

            return GenerateFile(exportData, format, "analytics");
        }

        private ActionResult GenerateFile<T>(List<T> data, string? format, string filePrefix)
        {
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");

            if (format?.ToLowerInvariant() == "xlsx")
            {
                var bytes = exportService.ToExcel(data, filePrefix);
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{filePrefix}-export-{date}.xlsx");
            }

            var csvBytes = exportService.ToCsv(data);
            return File(csvBytes, "text/csv", $"{filePrefix}-export-{date}.csv");
        }

        private static string FormatDuration(TimeSpan duration)
        {
            int hours = (duration.Days * 24) + duration.Hours;
            return $"{hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }
    }
}
```

- [ ] **Step 5: Run controller tests**

```bash
dotnet test --filter "FullyQualifiedName~ExportControllerTest"
```
Expected: 4/4 pass

- [ ] **Step 6: Run all tests**

```bash
dotnet test
```
Expected: 54+ tests pass (50 existing + 4 export service + 4 controller)

- [ ] **Step 7: Commit**

```bash
git add Timinute/Server/Controllers/ExportController.cs Timinute/Server/Program.cs Timinute/Server.Tests/Controllers/ExportControllerTest.cs
git commit -m "feat: add ExportController with tasks, projects, and analytics endpoints"
```

---

### Task 6: Final verification

- [ ] **Step 1: Clean build**

```bash
dotnet clean Timinute.sln && dotnet build Timinute.sln
```
Expected: 0 errors

- [ ] **Step 2: Run full test suite**

```bash
dotnet test --verbosity normal
```
Expected: All tests pass (58 total: 50 existing + 4 export service + 4 controller)

- [ ] **Step 3: Verify git status**

```bash
git status
```
Expected: Clean working tree
