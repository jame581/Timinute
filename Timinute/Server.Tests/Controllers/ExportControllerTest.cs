using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using System.Threading.Tasks;
using Timinute.Server.Controllers;
using Timinute.Server.Data;
using Timinute.Server.Repository;
using Timinute.Server.Services;
using Timinute.Server.Tests.Helpers;
using Xunit;

namespace Timinute.Server.Tests.Controllers
{
    public class ExportControllerTest : ControllerTestBase<ExportController>
    {
        private readonly Mock<ILogger<ExportController>> _loggerMock;
        private readonly IExportService _exportService;

        private const string _databaseName = "ExportController_Test_DB";

        public ExportControllerTest()
        {
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
            Assert.EndsWith(".xlsx", fileResult.FileDownloadName);
        }

        [Fact]
        public async Task Export_Tasks_With_DateRange_Filters()
        {
            ExportController controller = await CreateController();

            var from = new System.DateTime(2021, 10, 1);
            var to = new System.DateTime(2021, 10, 31);

            var actionResult = await controller.ExportTasks("csv", from, to, null, null);

            var fileResult = actionResult as FileContentResult;
            Assert.NotNull(fileResult);

            var csv = System.Text.Encoding.UTF8.GetString(fileResult!.FileContents);
            var lines = csv.Trim().Split('\n');

            // ApplicationUser1 has 4 tasks, all on 2021-10-01
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

        [Fact]
        public async Task Export_Projects_Csv_Returns_File_With_Grouped_Data()
        {
            ExportController controller = await CreateController();

            var actionResult = await controller.ExportProjects("csv", null, null, null);

            Assert.NotNull(actionResult);
            Assert.IsType<FileContentResult>(actionResult);

            var fileResult = actionResult as FileContentResult;
            Assert.NotNull(fileResult);
            Assert.Equal("text/csv", fileResult!.ContentType);
            Assert.Contains("projects-export-", fileResult.FileDownloadName);

            var csv = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
            var lines = csv.Trim().Split('\n');

            // ApplicationUser1 has tasks in ProjectId1 + some without project
            Assert.True(lines.Length >= 2); // header + at least 1 project row
            Assert.Contains("ProjectName", lines[0]);
            Assert.Contains("TotalHours", lines[0]);
            Assert.Contains("TaskCount", lines[0]);
        }

        [Fact]
        public async Task Export_Analytics_Csv_Returns_File_With_Monthly_Data()
        {
            ExportController controller = await CreateController();

            var actionResult = await controller.ExportAnalytics("csv", null, null);

            Assert.NotNull(actionResult);
            Assert.IsType<FileContentResult>(actionResult);

            var fileResult = actionResult as FileContentResult;
            Assert.NotNull(fileResult);
            Assert.Equal("text/csv", fileResult!.ContentType);
            Assert.Contains("analytics-export-", fileResult.FileDownloadName);

            var csv = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
            var lines = csv.Trim().Split('\n');

            Assert.True(lines.Length >= 2); // header + at least 1 month
            Assert.Contains("Month", lines[0]);
            Assert.Contains("TotalHours", lines[0]);
            Assert.Contains("TopProject", lines[0]);
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
