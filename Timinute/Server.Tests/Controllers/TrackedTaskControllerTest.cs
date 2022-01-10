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
using Timinute.Shared.Dtos.TrackedTask;
using Xunit;

namespace Timinute.Server.Tests.Controllers
{
    public class TrackedTaskControllerTest : ControllerTestBase<TrackedTaskController>
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
            TrackedTaskController controller = await CreateController();

            var actionResult = await controller.GetTrackedTasks();

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<IEnumerable<TrackedTaskDto>>(okResult!.Value);
            var trackedTasks = okResult.Value as IList<TrackedTaskDto>;

            Assert.NotNull(trackedTasks);

            Assert.Collection(trackedTasks,
            item => Assert.Contains("TrackedTaskId1", trackedTasks![0].TaskId),
            item => Assert.Contains("TrackedTaskId2", trackedTasks![1].TaskId),
            item => Assert.Contains("TrackedTaskId3", trackedTasks![2].TaskId));
        }

        [Fact]
        public async Task Get_TrackedTask_By_Id()
        {
            TrackedTaskController controller = await CreateController();

            var actionResult = await controller.GetTrackedTask("TrackedTaskId1");

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<TrackedTaskDto>(okResult!.Value);
            var trackedTask = okResult.Value as TrackedTaskDto;

            Assert.NotNull(trackedTask);
            Assert.Equal("TrackedTaskId1", trackedTask!.TaskId);
            Assert.Equal("Task 1", trackedTask.Name);
        }

        [Fact]
        public async Task Get_Not_Existing_TrackedTask_Test()
        {
            TrackedTaskController controller = await CreateController();

            var actionResult = await controller.GetTrackedTask("NonExistentID");

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<NotFoundObjectResult>(actionResult.Result);

            var notFoundResult = actionResult.Result as NotFoundObjectResult;
            Assert.NotNull(notFoundResult);

            Assert.Equal(404, notFoundResult!.StatusCode);
            Assert.Equal("Tracked task not found!", notFoundResult.Value);
        }

        [Fact]
        public async Task Delete_Existing_TrackedTask_Test()
        {
            TrackedTaskController controller = await CreateController();

            var trackedTaskToDelete = new TrackedTaskDto() { TaskId = "TrackedTaskId1" };

            var actionResult = await controller.DeleteTrackedTask(trackedTaskToDelete.TaskId);

            Assert.NotNull(actionResult);
            Assert.IsType<NoContentResult>(actionResult);
        }

        [Fact]
        public async Task Delete_Not_Existing_TrackedTask_Test()
        {
            TrackedTaskController controller = await CreateController();

            var trackedTaskToDelete = new TrackedTaskDto() { TaskId = "NonExistentID" };

            var actionResult = await controller.DeleteTrackedTask(trackedTaskToDelete.TaskId);

            //Assert  
            Assert.NotNull(actionResult);
            Assert.IsType<NotFoundObjectResult>(actionResult);
            var typedResult = actionResult as NotFoundObjectResult;
            Assert.Equal("Tracked task not found!", typedResult!.Value);
            Assert.Equal(404, typedResult.StatusCode);
        }

        [Fact]
        public async Task Update_Existing_TrackedTask_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "UpdateTest");

            TrackedTaskController controller = await CreateController(applicationDbContext);

            DateTime startDate = DateTime.Now;

            // create category with the same ID but updated data
            var trackedTaskToUpdate = new UpdateTrackedTaskDto
            {
                TaskId = "TrackedTaskId2",
                Name = "Task 22",
                StartDate = startDate,
            };

            var actionResult = await controller.UpdateTrackedTask(trackedTaskToUpdate);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);

            var updatedOkResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(updatedOkResult);
            Assert.IsAssignableFrom<TrackedTaskDto>(updatedOkResult!.Value);

            var updatedTrackedTask = updatedOkResult.Value as TrackedTaskDto;
            Assert.NotNull(updatedTrackedTask);

            Assert.Equal(trackedTaskToUpdate.TaskId, updatedTrackedTask!.TaskId);
            Assert.Equal(trackedTaskToUpdate.Name, updatedTrackedTask.Name);
            Assert.Equal(trackedTaskToUpdate.StartDate, updatedTrackedTask.StartDate.ToLocalTime());
        }

        [Fact]
        public async Task Update_Not_Existing_TrackedTask_Test()
        {
            TrackedTaskController controller = await CreateController();

            DateTime startDate = DateTime.Now;

            // create category with the same ID but updated data
            var trackedTaskToUpdate = new UpdateTrackedTaskDto
            {
                TaskId = "NonExistID",
                Name = "Task 1",
                StartDate = startDate,
            };

            var actionResult = await controller.UpdateTrackedTask(trackedTaskToUpdate);

            // Assert
            Assert.NotNull(actionResult);
            Assert.IsType<NotFoundObjectResult>(actionResult.Result);

            var notFoundResult = actionResult.Result as NotFoundObjectResult;
            Assert.NotNull(notFoundResult);
            Assert.Equal(404, notFoundResult!.StatusCode);
            Assert.Equal("Tracked task not found!", notFoundResult.Value);
        }

        [Fact]
        public async Task Create_New_TrackedTask_Test()
        {
            TrackedTaskController controller = await CreateController();

            DateTime startDate = DateTime.Now;

            // create category with the same ID but updated data
            var trackedTaskToCreate = new CreateTrackedTaskDto
            {
                Name = "New Task",
                StartDate = startDate,
                Duration = TimeSpan.FromHours(2),
            };

            var actionResult = await controller.CreateTrackedTask(trackedTaskToCreate);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);

            var okActionResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okActionResult);

            Assert.IsAssignableFrom<TrackedTaskDto>(okActionResult!.Value);
            var newlyCreatedTrackedTask = okActionResult!.Value as TrackedTaskDto;
            Assert.NotNull(newlyCreatedTrackedTask);

            Assert.Equal(trackedTaskToCreate.Name, newlyCreatedTrackedTask!.Name);
            Assert.Equal(trackedTaskToCreate.StartDate, newlyCreatedTrackedTask!.StartDate.ToLocalTime());
            Assert.Equal(trackedTaskToCreate.Duration, newlyCreatedTrackedTask!.Duration);
        }

        protected override async Task<TrackedTaskController> CreateController(ApplicationDbContext? applicationDbContext = null)
        {
            if (applicationDbContext == null)
            {
                applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName);
            }

            var repositoryFactory = new RepositoryFactory(applicationDbContext);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                                        new Claim("sub", "ApplicationUser1"),
                                        new Claim(ClaimTypes.Name, "test1@email.com")
                                        }
            ));

            TrackedTaskController controller = new(repositoryFactory, _mapper, _loggerMock.Object)
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
