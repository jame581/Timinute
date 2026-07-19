using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Timinute.Server.Controllers;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Server.Services.App;
using Timinute.Server.Tests.Helpers;
using Timinute.Shared.Dtos.Analytics;
using Timinute.Shared.Dtos.Dashboard;
using Xunit;

namespace Timinute.Server.Tests.Controllers
{
    public class AnalyticsControllerTest : ControllerTestBase<AnalyticsController>
    {
        private readonly IMapper _mapper;
        private readonly Mock<ILogger<AnalyticsController>> _loggerMock;

        private const string _databaseName = "AnalyticsController_Test_DB";

        public AnalyticsControllerTest()
        {
            var configExpression = new MapperConfigurationExpression();
            configExpression.AddProfile<MappingProfile>();
            var configuration = new MapperConfiguration(configExpression, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            _mapper = new Mapper(configuration);

            _loggerMock = new Mock<ILogger<AnalyticsController>>();
        }

        [Fact]
        public async Task Get_Amount_Work_Time_Last_Month_Test()
        {
            AnalyticsController controller = await CreateController();

            DateTimeOffset lastMonth = DateTimeOffset.UtcNow.AddMonths(-1);

            var actionResult = await controller.GetAmountWorkTimeByMonth(new AmountWorkTimeByMonthDto { Year = lastMonth.Year, Month = lastMonth.Month });

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<AmountOfWorkTimeDto>(okResult!.Value);
            var amountOfWorkTimeDto = okResult.Value as AmountOfWorkTimeDto;

            Assert.NotNull(amountOfWorkTimeDto);

            Assert.Equal("Project 1003", amountOfWorkTimeDto.TopProject);
            Assert.Equal(TimeSpan.FromHours(11).TotalSeconds, amountOfWorkTimeDto.TopProjectAmounTime);
            Assert.Equal(TimeSpan.FromSeconds(amountOfWorkTimeDto.TopProjectAmounTime).ToString(@"hh\:mm\:ss"), amountOfWorkTimeDto.TopProjectAmounTimeText);

            Assert.Equal("28:00:00", amountOfWorkTimeDto.AmountWorkTimeText);
            Assert.Equal(TimeSpan.FromHours(28).TotalSeconds, amountOfWorkTimeDto.AmountWorkTime);
        }

        [Fact]
        public async Task Get_Amount_Work_Time_This_Month_Test()
        {
            AnalyticsController controller = await CreateController();

            DateTimeOffset thisMonth = DateTimeOffset.UtcNow;

            var actionResult = await controller.GetAmountWorkTimeByMonth(new AmountWorkTimeByMonthDto { Year = thisMonth.Year, Month = thisMonth.Month });

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<AmountOfWorkTimeDto>(okResult!.Value);
            var amountOfWorkTimeDto = okResult.Value as AmountOfWorkTimeDto;

            Assert.NotNull(amountOfWorkTimeDto);

            Assert.Equal("Project 1004", amountOfWorkTimeDto.TopProject);
            Assert.Equal(TimeSpan.FromHours(10).TotalSeconds, amountOfWorkTimeDto.TopProjectAmounTime);
            Assert.Equal(TimeSpan.FromSeconds(amountOfWorkTimeDto.TopProjectAmounTime).ToString(@"hh\:mm\:ss"), amountOfWorkTimeDto.TopProjectAmounTimeText);

            Assert.Equal("10:00:00", amountOfWorkTimeDto.AmountWorkTimeText);
            Assert.Equal(TimeSpan.FromHours(10).TotalSeconds, amountOfWorkTimeDto.AmountWorkTime);
        }

        [Fact]
        public async Task Get_Work_Time_Per_Months_Test()
        {
            AnalyticsController controller = await CreateController();

            var actionResult = await controller.GetWorkTimePerMonths();

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<List<WorkTimePerMonthDto>>(okResult!.Value);
            var workTimePerMonths = okResult.Value as List<WorkTimePerMonthDto>;

            Assert.NotNull(workTimePerMonths);
            Assert.Equal(2, workTimePerMonths!.Count);

            var thisMonth = DateTimeOffset.UtcNow;
            var lastMonth = thisMonth.AddMonths(-1);
            var thisMonthLabel = new DateTimeOffset(thisMonth.Year, thisMonth.Month, 1, 0, 0, 0, TimeSpan.Zero).ToString("yyyy MMM");
            var lastMonthLabel = new DateTimeOffset(lastMonth.Year, lastMonth.Month, 1, 0, 0, 0, TimeSpan.Zero).ToString("yyyy MMM");

            Assert.Contains(workTimePerMonths, w =>
                w.Time == thisMonthLabel &&
                w.WorkTimeInSeconds == TimeSpan.FromHours(10).TotalSeconds);

            Assert.Contains(workTimePerMonths, w =>
                w.Time == lastMonthLabel &&
                w.WorkTimeInSeconds == TimeSpan.FromHours(28).TotalSeconds);
        }

        [Fact]
        public async Task Get_Work_Time_Per_Months()
        {
            AnalyticsController controller = await CreateController();

            var actionResult = await controller.GetProjectWorkTimePerMonths();

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<List<ProjectDataItemsPerMonthDto>>(okResult!.Value);
            var projectDataItemsPerMonthDtos = okResult.Value as List<ProjectDataItemsPerMonthDto>;

            Assert.NotNull(projectDataItemsPerMonthDtos);

            var today = DateTimeOffset.UtcNow;
            var month = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var first = month.AddMonths(-1);

            Assert.Equal(2, projectDataItemsPerMonthDtos!.Count);

            // TODO: Check why GitHub action have problem with this
            //Assert.Collection(projectDataItemsPerMonthDtos,
            //    item =>
            //    {
            //        Assert.Equal(first.Year, item.Time.Year);
            //        Assert.Equal(first.Month, item.Time.Month);
            //        Assert.Equal(7, item.ProjectDataItems.Count);
            //    },
            //    item =>
            //    {
            //        Assert.Equal(month.Year, item.Time.Year);
            //        Assert.Equal(month.Month, item.Time.Month);
            //        Assert.Equal(4, item.ProjectDataItems.Count);
            //    });
        }

        [Fact]
        public async Task Get_Project_Work_Time()
        {
            AnalyticsController controller = await CreateController();

            var actionResult = await controller.GetProjectWorkTime();

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<List<ProjectDataItemDto>>(okResult!.Value);
            var projectDataItems = okResult.Value as List<ProjectDataItemDto>;

            Assert.NotNull(projectDataItems);

            Assert.Collection(projectDataItems,
                item =>
                {
                    Assert.Equal("ProjectId1003", item.ProjectId);
                    Assert.Equal(TimeSpan.FromHours(11), item.Time);
                },
                item =>
                {
                    Assert.Equal("ProjectId1004", item.ProjectId);
                    Assert.Equal(TimeSpan.FromHours(10), item.Time);
                }, item =>
                {
                    Assert.Equal("ProjectId1002", item.ProjectId);
                    Assert.Equal(TimeSpan.FromHours(7), item.Time);
                }, item =>
                {
                    Assert.Equal("None", item.ProjectId);
                    Assert.Equal(TimeSpan.FromHours(7), item.Time);
                }, item =>
                {
                    Assert.Equal("ProjectId1001", item.ProjectId);
                    Assert.Equal(TimeSpan.FromHours(3), item.Time);
                });
        }


        private static AnalyticsRangeDto LastMonthRange()
        {
            var now = DateTimeOffset.UtcNow;
            var thisMonthFirst = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            return new AnalyticsRangeDto
            {
                From = thisMonthFirst.AddMonths(-1),
                To = thisMonthFirst.AddSeconds(-1),
                TzOffsetMinutes = 0
            };
        }

        [Fact]
        public async Task Get_Range_Summary_Test()
        {
            var controller = await CreateController();

            var actionResult = await controller.GetRangeSummary(LastMonthRange());

            var okResult = Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var summary = Assert.IsAssignableFrom<AnalyticsSummaryDto>(okResult.Value);

            Assert.Equal(TimeSpan.FromHours(28), summary.TotalDuration);
            Assert.Equal(7, summary.TaskCount);
            Assert.Equal(1, summary.ActiveDays);           // all 7 tasks start on the same day
            Assert.Equal(TimeSpan.FromHours(28), summary.AveragePerActiveDay);
            Assert.True(summary.WeekdayCount >= 20);        // a full month always has ≥20 weekdays
            Assert.Equal(TimeSpan.FromHours((double)(8.0m * summary.WeekdayCount)), summary.TargetDuration);
        }

        [Fact]
        public async Task Get_Daily_Breakdown_Test()
        {
            var controller = await CreateController();

            var actionResult = await controller.GetDailyBreakdown(LastMonthRange());

            var okResult = Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var days = Assert.IsAssignableFrom<IEnumerable<DailyAnalyticsDto>>(okResult.Value).ToList();

            var day = Assert.Single(days);
            Assert.Equal(TimeSpan.FromHours(28), day.TotalDuration);
            Assert.Equal(7, day.TaskCount);
        }

        [Fact]
        public async Task Get_Daily_Breakdown_TzOffset_Shifts_Day_Test()
        {
            // Tasks start at 00:00 UTC on the 1st; with tz -60 the local day is the
            // last day of the previous month.
            var controller = await CreateController();
            var range = LastMonthRange();
            range.TzOffsetMinutes = -60;

            var actionResult = await controller.GetDailyBreakdown(range);

            var okResult = Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var days = Assert.IsAssignableFrom<IEnumerable<DailyAnalyticsDto>>(okResult.Value).ToList();

            var day = Assert.Single(days);
            var utcDay = range.From.UtcDateTime.Date;
            Assert.Equal(utcDay.AddDays(-1), day.Date);
        }

        [Fact]
        public async Task Get_Project_Breakdown_Includes_None_Bucket_Test()
        {
            var controller = await CreateController();

            var actionResult = await controller.GetProjectBreakdown(LastMonthRange());

            var okResult = Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var projects = Assert.IsAssignableFrom<IEnumerable<ProjectAnalyticsDto>>(okResult.Value).ToList();

            // Projects 1001 (3h), 1002 (7h), 1003 (11h) + the no-project task (7h)
            Assert.Equal(4, projects.Count);
            Assert.Equal(projects.OrderByDescending(p => p.TotalDuration).Select(p => p.ProjectId), projects.Select(p => p.ProjectId));

            var none = Assert.Single(projects, p => p.ProjectId == "_none");
            Assert.Equal(TimeSpan.FromHours(7), none.TotalDuration);
            Assert.Equal("No project", none.Name);

            var p1003 = Assert.Single(projects, p => p.ProjectId == "ProjectId1003");
            Assert.Equal(TimeSpan.FromHours(11), p1003.TotalDuration);
            Assert.Equal(2, p1003.TaskCount);
        }

        [Fact]
        public async Task Get_Tag_Breakdown_Buckets_Untagged_Test()
        {
            // The analytics fixture has no tags, so everything lands in _untagged.
            var controller = await CreateController();

            var actionResult = await controller.GetTagBreakdown(LastMonthRange());

            var okResult = Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var tags = Assert.IsAssignableFrom<IEnumerable<TagAnalyticsDto>>(okResult.Value).ToList();

            var untagged = Assert.Single(tags);
            Assert.Equal("_untagged", untagged.TagId);
            Assert.Equal(TimeSpan.FromHours(28), untagged.TotalDuration);
            Assert.Equal(7, untagged.TaskCount);
        }

        [Fact]
        public async Task Get_Range_Summary_Excludes_SoftDeleted_Tasks_Test()
        {
            var applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "SoftDeleteSummary", analyticsTest: true);

            var range = LastMonthRange();
            var softDeletedTask = new TrackedTask
            {
                TaskId = "TrackedTaskId1099",
                Name = "Soft Deleted Task",
                UserId = "ApplicationUser1000",
                StartDate = range.From,
                EndDate = range.From.AddHours(1),
                Duration = TimeSpan.FromHours(1),
                DeletedAt = DateTimeOffset.UtcNow
            };

            applicationDbContext.TrackedTasks.Add(softDeletedTask);
            await applicationDbContext.SaveChangesAsync();
            applicationDbContext.ChangeTracker.Clear();

            var controller = await CreateController(applicationDbContext);

            var actionResult = await controller.GetRangeSummary(range);

            var okResult = Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var summary = Assert.IsAssignableFrom<AnalyticsSummaryDto>(okResult.Value);

            Assert.Equal(7, summary.TaskCount);
            Assert.Equal(TimeSpan.FromHours(28), summary.TotalDuration);
        }

        [Fact]
        public async Task Get_Range_Summary_Other_Users_Data_Excluded_Test()
        {
            // ApplicationUser1000's range summary must not include the base fixture's
            // 2021 tasks (they belong to ApplicationUser1/2/3 anyway) — query a range
            // covering 2021 and expect zero.
            var controller = await CreateController();
            var range = new AnalyticsRangeDto
            {
                From = new DateTimeOffset(2021, 9, 1, 0, 0, 0, TimeSpan.Zero),
                To = new DateTimeOffset(2021, 11, 1, 0, 0, 0, TimeSpan.Zero),
                TzOffsetMinutes = 0
            };

            var actionResult = await controller.GetRangeSummary(range);

            var okResult = Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var summary = Assert.IsAssignableFrom<AnalyticsSummaryDto>(okResult.Value);
            Assert.Equal(0, summary.TaskCount);
            Assert.Equal(TimeSpan.Zero, summary.TotalDuration);
        }

        protected override async Task<AnalyticsController> CreateController(ApplicationDbContext? applicationDbContext = null, string userId = "ApplicationUser1")
        {
            if (applicationDbContext == null)
            {
                applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName, analyticsTest: true);
            }

            var repositoryFactory = new RepositoryFactory(applicationDbContext);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                                        new Claim("sub", "ApplicationUser1000"),
                                        new Claim(ClaimTypes.Name, "test1000@email.com")
                                        }
            ));

            AnalyticsController controller = new(repositoryFactory, _mapper, _loggerMock.Object, applicationDbContext,
                new AnalyticsAppService(applicationDbContext))
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
