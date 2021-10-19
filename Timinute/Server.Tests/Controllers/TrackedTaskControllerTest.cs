using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Timinute.Server.Controllers;
using Timinute.Server.Repository;
using Timinute.Server.Tests.Helpers;
using Timinute.Shared.Dtos.TrackedTask;
using Xunit;

namespace Timinute.Server.Tests.Controllers
{
    public class TrackedTaskControllerTest
    {
        private readonly IMapper _mapper;
        private readonly Mock<ILogger<TrackedTaskController>> _loggerMock;

        private const string _databaseName = "TrackedTaskController_Test_DB";
        public TrackedTaskControllerTest()
        {
            var myProfile = new MappingProfile();
            var configuration = new MapperConfiguration(cfg => cfg.AddProfile(myProfile));
            _mapper = new Mapper(configuration);

            _loggerMock = new Mock<ILogger<TrackedTaskController>>();
        }

        [Fact]
        public async Task Get_All_TrackedTasks_Test()
        {
            await using var dbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + 1);
            var repositoryFactory = new RepositoryFactory(dbContext);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                                        new Claim("sub", "ApplicationUser1"),
                                        new Claim(ClaimTypes.Name, "test1@email.com")
                                        }
            ));

            TrackedTaskController controller = new TrackedTaskController(repositoryFactory, _mapper, _loggerMock.Object);
            controller.ControllerContext = new ControllerContext();
            controller.ControllerContext.HttpContext = new DefaultHttpContext { User = user };

            var actionResult = await controller.GetTrackedTasks();

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<IEnumerable<TrackedTaskDto>>(okResult.Value);
            var trackedTasks = okResult.Value as IList<TrackedTaskDto>;

            Assert.NotNull(trackedTasks);

            Assert.Collection(trackedTasks,
            item => Assert.Contains("TrackedTaskId1", trackedTasks[0].TaskId),
            item => Assert.Contains("TrackedTaskId2", trackedTasks[1].TaskId),
            item => Assert.Contains("TrackedTaskId3", trackedTasks[2].TaskId));
        }        
    }
}
