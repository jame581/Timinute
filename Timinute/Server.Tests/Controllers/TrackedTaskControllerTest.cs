using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Timinute.Server.Controllers;
using Timinute.Server.Data;
using Timinute.Server.Models.Paging;
using Timinute.Server.Repository;
using Timinute.Server.Tests.Helpers;
using Timinute.Shared.Dtos.TrackedTask;
using Timinute.Shared.Dtos.Trash;
using Xunit;

namespace Timinute.Server.Tests.Controllers
{
    public class TrackedTaskControllerTest : ControllerTestBase<TrackedTaskController>
    {
        private readonly IMapper _mapper;
        private readonly Mock<ILogger<TrackedTaskController>> _loggerMock;

        private PagingParameters pagingParameters;

        private const string _databaseName = "TrackedTaskController_Test_DB";
        public TrackedTaskControllerTest()
        {
            var configExpression = new MapperConfigurationExpression();
            configExpression.AddProfile<MappingProfile>();
            var configuration = new MapperConfiguration(configExpression, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            _mapper = new Mapper(configuration);

            _loggerMock = new Mock<ILogger<TrackedTaskController>>();

            pagingParameters = new PagingParameters()
            {
                PageSize = 100,
                PageNumber = 1,
            };
        }

        [Fact]
        public async Task Get_All_TrackedTasks_Test()
        {
            TrackedTaskController controller = await CreateController();

            pagingParameters.PageNumber = 1;
            pagingParameters.PageSize = 1000;

            var actionResult = await controller.GetTrackedTasks(pagingParameters);

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
            item => Assert.Contains("TrackedTaskId3", trackedTasks![2].TaskId),
            item => Assert.Contains("TrackedTaskId4", trackedTasks![3].TaskId));
        }

        [Fact]
        public async Task Get_First_Page_TrackedTasks_Test()
        {
            TrackedTaskController controller = await CreateController();

            pagingParameters.PageNumber = 1;
            pagingParameters.PageSize = 3;

            var actionResult = await controller.GetTrackedTasks(pagingParameters);

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
        public async Task Get_Second_Page_TrackedTasks_Test()
        {
            TrackedTaskController controller = await CreateController();

            pagingParameters.PageNumber = 2;
            pagingParameters.PageSize = 2;

            var actionResult = await controller.GetTrackedTasks(pagingParameters);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<IEnumerable<TrackedTaskDto>>(okResult!.Value);
            var trackedTasks = okResult.Value as IList<TrackedTaskDto>;

            Assert.NotNull(trackedTasks);

            Assert.Collection(trackedTasks,
            item => Assert.Contains("TrackedTaskId3", trackedTasks![0].TaskId),
            item => Assert.Contains("TrackedTaskId4", trackedTasks![1].TaskId));
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

            DateTimeOffset startDate = DateTimeOffset.UtcNow;

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
            Assert.Equal(trackedTaskToUpdate.StartDate.UtcDateTime, updatedTrackedTask.StartDate.UtcDateTime, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Update_Not_Existing_TrackedTask_Test()
        {
            TrackedTaskController controller = await CreateController();

            DateTimeOffset startDate = DateTimeOffset.UtcNow;

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

            DateTimeOffset startDate = DateTimeOffset.UtcNow;

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
            Assert.Equal(trackedTaskToCreate.StartDate.UtcDateTime, newlyCreatedTrackedTask!.StartDate.UtcDateTime, TimeSpan.FromSeconds(1));
            Assert.Equal(trackedTaskToCreate.Duration, newlyCreatedTrackedTask!.Duration);
        }

        [Fact]
        public async Task Update_TrackedTask_Another_User_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "UpdateAuthTest");
            TrackedTaskController controller = await CreateController(applicationDbContext, "ApplicationUser2");

            var trackedTaskToUpdate = new UpdateTrackedTaskDto
            {
                TaskId = "TrackedTaskId1",  // belongs to ApplicationUser1
                Name = "Hacked Name",
                StartDate = DateTimeOffset.UtcNow,
            };

            var actionResult = await controller.UpdateTrackedTask(trackedTaskToUpdate);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<NotFoundObjectResult>(actionResult.Result);

            var notFoundResult = actionResult.Result as NotFoundObjectResult;
            Assert.NotNull(notFoundResult);
            Assert.Equal("Tracked task not found!", notFoundResult!.Value);
        }

        [Fact]
        public async Task Get_TrackedTask_Another_User_Returns_NotFound_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "GetAuthTest");
            TrackedTaskController controller = await CreateController(applicationDbContext, "ApplicationUser10");

            var actionResult = await controller.GetTrackedTask("TrackedTaskId1");

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<NotFoundObjectResult>(actionResult.Result);

            var notFoundResult = actionResult.Result as NotFoundObjectResult;
            Assert.NotNull(notFoundResult);
            Assert.Equal("Tracked task not found!", notFoundResult!.Value);
        }

        [Fact]
        public async Task Update_TrackedTask_EndDate_Before_StartDate_Returns_BadRequest_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "EndDateTest");
            TrackedTaskController controller = await CreateController(applicationDbContext);

            var taskToUpdate = new UpdateTrackedTaskDto
            {
                TaskId = "TrackedTaskId1",
                Name = "Updated Task",
                StartDate = new DateTimeOffset(2021, 10, 1, 10, 0, 0, TimeSpan.Zero),
                EndDate = new DateTimeOffset(2021, 10, 1, 8, 0, 0, TimeSpan.Zero),
            };

            var actionResult = await controller.UpdateTrackedTask(taskToUpdate);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<BadRequestObjectResult>(actionResult.Result);

            var badRequestResult = actionResult.Result as BadRequestObjectResult;
            Assert.NotNull(badRequestResult);
            Assert.Equal("End date must be strictly after start date.", badRequestResult!.Value);
        }

        [Fact]
        public async Task Search_Tasks_By_DateRange()
        {
            TrackedTaskController controller = await CreateController();
            var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };
            var from = new DateTimeOffset(2021, 10, 1, 0, 0, 0, TimeSpan.Zero);
            var to = new DateTimeOffset(2021, 10, 31, 0, 0, 0, TimeSpan.Zero);

            var actionResult = await controller.SearchTrackedTasks(pagingParams, from, to, null, null);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;
            var tasks = okResult!.Value as IEnumerable<TrackedTaskDto>;
            Assert.NotNull(tasks);
            Assert.Equal(4, tasks!.Count());
        }

        [Fact]
        public async Task Search_Tasks_By_ProjectId()
        {
            TrackedTaskController controller = await CreateController();
            var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

            var actionResult = await controller.SearchTrackedTasks(pagingParams, null, null, "ProjectId1", null);

            Assert.NotNull(actionResult);
            var okResult = actionResult.Result as OkObjectResult;
            var tasks = okResult!.Value as IEnumerable<TrackedTaskDto>;
            Assert.NotNull(tasks);
            Assert.Equal(3, tasks!.Count());
        }

        [Fact]
        public async Task Search_Tasks_By_Name()
        {
            TrackedTaskController controller = await CreateController();
            var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

            var actionResult = await controller.SearchTrackedTasks(pagingParams, null, null, null, "Task 1");

            Assert.NotNull(actionResult);
            var okResult = actionResult.Result as OkObjectResult;
            var tasks = okResult!.Value as IEnumerable<TrackedTaskDto>;
            Assert.NotNull(tasks);
            Assert.Single(tasks!);
            Assert.Equal("TrackedTaskId1", tasks!.First().TaskId);
        }

        [Fact]
        public async Task Search_Tasks_Combined_Filters()
        {
            TrackedTaskController controller = await CreateController();
            var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };
            var from = new DateTimeOffset(2021, 10, 1, 0, 0, 0, TimeSpan.Zero);
            var to = new DateTimeOffset(2021, 10, 31, 0, 0, 0, TimeSpan.Zero);

            var actionResult = await controller.SearchTrackedTasks(pagingParams, from, to, "ProjectId1", "Task");

            Assert.NotNull(actionResult);
            var okResult = actionResult.Result as OkObjectResult;
            var tasks = okResult!.Value as IEnumerable<TrackedTaskDto>;
            Assert.NotNull(tasks);
            Assert.Equal(3, tasks!.Count());
        }

        [Fact]
        public async Task Search_Tasks_Another_User_Empty()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "SearchAuthTest");
            TrackedTaskController controller = await CreateController(applicationDbContext, "NonExistentUser");
            var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

            var actionResult = await controller.SearchTrackedTasks(pagingParams, null, null, null, null);

            Assert.NotNull(actionResult);
            var okResult = actionResult.Result as OkObjectResult;
            var tasks = okResult!.Value as IEnumerable<TrackedTaskDto>;
            Assert.NotNull(tasks);
            Assert.Empty(tasks!);
        }

        [Fact]
        public async Task Delete_TrackedTask_Soft_Deletes_Row_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "SoftDeleteTask");
            TrackedTaskController controller = await CreateController(applicationDbContext);

            var actionResult = await controller.DeleteTrackedTask("TrackedTaskId1");

            Assert.IsType<NoContentResult>(actionResult);

            var stillInDb = await applicationDbContext.TrackedTasks.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId1");
            Assert.NotNull(stillInDb);
            Assert.NotNull(stillInDb!.DeletedAt);

            var hidden = await applicationDbContext.TrackedTasks.FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId1");
            Assert.Null(hidden);
        }

        [Fact]
        public async Task Restore_TrackedTask_Returns_Task_To_Default_Query_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "RestoreTask");
            TrackedTaskController controller = await CreateController(applicationDbContext);

            await controller.DeleteTrackedTask("TrackedTaskId1");

            var actionResult = await controller.RestoreTrackedTask("TrackedTaskId1");
            Assert.IsType<NoContentResult>(actionResult);

            var restored = await applicationDbContext.TrackedTasks.FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId1");
            Assert.NotNull(restored);
            Assert.Null(restored!.DeletedAt);
        }

        [Fact]
        public async Task Restore_TrackedTask_Another_User_Returns_NotFound_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "RestoreTaskAuth");
            TrackedTaskController owner = await CreateController(applicationDbContext);
            await owner.DeleteTrackedTask("TrackedTaskId1");

            TrackedTaskController other = await CreateController(applicationDbContext, "ApplicationUser10");
            var actionResult = await other.RestoreTrackedTask("TrackedTaskId1");

            Assert.IsType<NotFoundObjectResult>(actionResult);
        }

        [Fact]
        public async Task GetTrash_TrackedTasks_Returns_Only_Deleted_Owned_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "TrashTask");
            TrackedTaskController controller = await CreateController(applicationDbContext);

            await controller.DeleteTrackedTask("TrackedTaskId1");
            await controller.DeleteTrackedTask("TrackedTaskId2");

            var actionResult = await controller.GetTrashTrackedTasks();

            Assert.NotNull(actionResult);
            var okResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okResult);
            var items = okResult!.Value as IEnumerable<TrashItemDto>;
            Assert.NotNull(items);
            var list = items!.ToList();
            Assert.Equal(2, list.Count);
            Assert.All(list, i => Assert.InRange(i.DaysRemaining, 29, 30));
        }

        [Fact]
        public async Task Purge_TrackedTask_Hard_Removes_Row_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "PurgeTask");
            TrackedTaskController controller = await CreateController(applicationDbContext);

            await controller.DeleteTrackedTask("TrackedTaskId1");

            var actionResult = await controller.PurgeTrackedTask("TrackedTaskId1");
            Assert.IsType<NoContentResult>(actionResult);

            var row = await applicationDbContext.TrackedTasks.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId1");
            Assert.Null(row);
        }

        [Fact]
        public async Task Purge_TrackedTask_Another_User_Returns_NotFound_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "PurgeTaskAuth");
            TrackedTaskController owner = await CreateController(applicationDbContext);
            await owner.DeleteTrackedTask("TrackedTaskId1");

            TrackedTaskController other = await CreateController(applicationDbContext, "ApplicationUser10");
            var actionResult = await other.PurgeTrackedTask("TrackedTaskId1");

            Assert.IsType<NotFoundObjectResult>(actionResult);
        }

        protected override async Task<TrackedTaskController> CreateController(ApplicationDbContext? applicationDbContext = null, string userId = "ApplicationUser1")
        {
            if (applicationDbContext == null)
            {
                applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName);
            }

            var repositoryFactory = new RepositoryFactory(applicationDbContext);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                                        new Claim("sub", userId),
                                        new Claim(ClaimTypes.Name, "test1@email.com")
                                        }
            ));

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["TrashRetention:Days"] = "30" })
                .Build();

            TrackedTaskController controller = new(repositoryFactory, _mapper, _loggerMock.Object, configuration)
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
