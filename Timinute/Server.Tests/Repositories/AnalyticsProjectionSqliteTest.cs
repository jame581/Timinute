using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Timinute.Server.Controllers;
using Timinute.Server.Data;
using Timinute.Server.Repository;
using Timinute.Server.Tests.Helpers;
using Timinute.Shared.Dtos.Analytics;
using Xunit;

namespace Timinute.Server.Tests.Repositories
{
    // Proves the analytics projection queries translate to SQL. InMemory
    // would silently client-evaluate a broken projection; SQLite throws.
    public class AnalyticsProjectionSqliteTest
    {
        // HasData seed user (ApplicationDbContext.SeedUserId1) — owns the
        // Jan/Feb 2022 seeded tasks that EnsureCreatedAsync applies on SQLite.
        private const string SeedUserId1 = "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d";

        private static readonly AnalyticsRangeDto Seed2022Range = new()
        {
            From = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
            To = new DateTimeOffset(2022, 3, 1, 0, 0, 0, TimeSpan.Zero),
            TzOffsetMinutes = 60
        };

        [Fact]
        public async Task All_Four_Range_Endpoints_Translate_On_Sqlite()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var db = await TestHelper.GetSqliteApplicationDbContext(connection);
            var controller = CreateController(db);

            var summary = await controller.GetRangeSummary(Seed2022Range);
            var daily = await controller.GetDailyBreakdown(Seed2022Range);
            var projects = await controller.GetProjectBreakdown(Seed2022Range);
            var tags = await controller.GetTagBreakdown(Seed2022Range);

            // HasData seeds SeedUserId1 with 3 tasks in Jan/Feb 2022 (2h + 4h + 3h).
            var summaryDto = (AnalyticsSummaryDto)((OkObjectResult)summary.Result!).Value!;
            Assert.True(summaryDto.TaskCount > 0);
            Assert.True(summaryDto.TotalDuration > TimeSpan.Zero);

            var dailyList = ((IEnumerable<DailyAnalyticsDto>)((OkObjectResult)daily.Result!).Value!).ToList();
            Assert.NotEmpty(dailyList);

            var projectList = ((IEnumerable<ProjectAnalyticsDto>)((OkObjectResult)projects.Result!).Value!).ToList();
            Assert.NotEmpty(projectList);

            var tagList = ((IEnumerable<TagAnalyticsDto>)((OkObjectResult)tags.Result!).Value!).ToList();
            Assert.NotEmpty(tagList);
        }

        private static AnalyticsController CreateController(ApplicationDbContext db)
        {
            var configExpression = new MapperConfigurationExpression();
            configExpression.AddProfile<MappingProfile>();
            var mapper = new Mapper(new MapperConfiguration(configExpression,
                Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance));

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", SeedUserId1),
                new Claim(ClaimTypes.Name, "test1@email.com")
            }));

            return new AnalyticsController(new RepositoryFactory(db), mapper,
                new Mock<ILogger<AnalyticsController>>().Object, db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                }
            };
        }
    }
}
