using System.Collections.Generic;
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

            Assert.Equal(3, lines.Length);
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

            Assert.Single(lines);
            Assert.Contains("Name", lines[0]);
        }
    }
}
