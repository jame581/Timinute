using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Timinute.Server.Controllers;
using Timinute.Server.Data;
using Timinute.Server.Repository;
using Timinute.Server.Tests.Helpers;
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
            var myProfile = new MappingProfile();
            var configuration = new MapperConfiguration(cfg => cfg.AddProfile(myProfile));
            _mapper = new Mapper(configuration);

            _loggerMock = new Mock<ILogger<AnalyticsController>>();
        }

        [Fact]
        public async Task Get_Amount_Work_Time_Last_Month_Test()
        {
            AnalyticsController controller = await CreateController();

            var actionResult = await controller.GetAmountWorkTimeLastMonth();

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<AmountOfWorkTimeDto>(okResult!.Value);
            var amountOfWorkTimeDto = okResult.Value as AmountOfWorkTimeDto;

            Assert.NotNull(amountOfWorkTimeDto);

            Assert.Equal("Project 1003", amountOfWorkTimeDto.TopProject);
            Assert.Equal(TimeSpan.FromHours(5).TotalSeconds + TimeSpan.FromHours(6).TotalSeconds, amountOfWorkTimeDto.TopProjectAmounTime);
            Assert.Equal(TimeSpan.FromSeconds(amountOfWorkTimeDto.TopProjectAmounTime).ToString(@"hh\:mm\:ss"), amountOfWorkTimeDto.TopProjectAmounTimeText);

            Assert.Equal("28:00:00", amountOfWorkTimeDto.AmountWorkTimeText);
            Assert.Equal(TimeSpan.FromHours(28).TotalSeconds, amountOfWorkTimeDto.AmountWorkTime);
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

            var today = DateTime.Today;
            var month = new DateTime(today.Year, today.Month, 1);
            var first = month.AddMonths(-1);

            Assert.Collection(projectDataItemsPerMonthDtos,
                item =>
                {
                    Assert.Equal(first.Year, item.Time.Year);
                    Assert.Equal(first.Month, item.Time.Month);
                    Assert.Equal(7, item.ProjectDataItems.Count);
                },
                item =>
                {
                    Assert.Equal(month.Year, item.Time.Year);
                    Assert.Equal(month.Month, item.Time.Month);
                    Assert.Equal(4, item.ProjectDataItems.Count);
                });
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
                    Assert.Equal("None", item.ProjectId);
                    Assert.Equal(TimeSpan.FromHours(7), item.Time);
                }, item =>
                {
                    Assert.Equal("ProjectId1002", item.ProjectId);
                    Assert.Equal(TimeSpan.FromHours(7), item.Time);
                }, item =>
                {
                    Assert.Equal("ProjectId1001", item.ProjectId);
                    Assert.Equal(TimeSpan.FromHours(3), item.Time);
                });
        }


        protected override async Task<AnalyticsController> CreateController(ApplicationDbContext? applicationDbContext = null)
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

            AnalyticsController controller = new(repositoryFactory, _mapper, _loggerMock.Object)
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
