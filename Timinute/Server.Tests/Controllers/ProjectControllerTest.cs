using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Timinute.Server.Controllers;
using Timinute.Server.Data;
using Timinute.Server.Models.Paging;
using Timinute.Server.Repository;
using Timinute.Server.Tests.Helpers;
using Timinute.Shared.Dtos.Project;
using Timinute.Shared.Dtos.Trash;
using Xunit;

namespace Timinute.Server.Tests.Controllers
{
    public class ProjectControllerTest : ControllerTestBase<ProjectController>
    {
        private readonly IMapper _mapper;
        private readonly Mock<ILogger<ProjectController>> _loggerMock;

        private PagingParameters pagingParameters;

        private const string _databaseName = "ProjectController_Test_DB";
        public ProjectControllerTest()
        {
            var configExpression = new MapperConfigurationExpression();
            configExpression.AddProfile<MappingProfile>();
            var configuration = new MapperConfiguration(configExpression, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            _mapper = new Mapper(configuration);

            _loggerMock = new Mock<ILogger<ProjectController>>();

            pagingParameters = new PagingParameters()
            {
                PageSize = 100,
                PageNumber = 1,
            };
        }

        [Fact]
        public async Task Get_All_Projects_Test()
        {
            ProjectController controller = await CreateController();

            var actionResult = await controller.GetProjects(pagingParameters);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<IEnumerable<ProjectDto>>(okResult!.Value);
            var projectDtos = okResult.Value as IList<ProjectDto>;

            Assert.NotNull(projectDtos);

            Assert.Collection(projectDtos,
            item => Assert.Contains("ProjectId1", projectDtos![0].ProjectId),
            item => Assert.Contains("ProjectId4", projectDtos![1].ProjectId),
            item => Assert.Contains("ProjectId5", projectDtos![2].ProjectId));
        }

        [Fact]
        public async Task Get_Project_By_Id()
        {
            ProjectController controller = await CreateController();

            var actionResult = await controller.GetProject("ProjectId1");

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<ProjectDto>(okResult!.Value);
            var project = okResult.Value as ProjectDto;

            Assert.NotNull(project);
            Assert.Equal("ProjectId1", project!.ProjectId);
            Assert.Equal("Project 1", project!.Name);
        }

        [Fact]
        public async Task Get_Not_Existing_Project_Test()
        {
            ProjectController controller = await CreateController();

            var actionResult = await controller.GetProject("NonExistentID");

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<NotFoundObjectResult>(actionResult.Result);

            var notFoundResult = actionResult.Result as NotFoundObjectResult;
            Assert.NotNull(notFoundResult);

            Assert.Equal(404, notFoundResult!.StatusCode);
            Assert.Equal("Project not found!", notFoundResult!.Value);
        }

        [Fact]
        public async Task Delete_Existing_Project_Test()
        {
            ProjectController controller = await CreateController();

            var projectToDelete = new ProjectDto() { ProjectId = "ProjectId1" };

            var actionResult = await controller.DeleteProject(projectToDelete.ProjectId);

            Assert.NotNull(actionResult);
            Assert.IsType<NoContentResult>(actionResult);
        }

        [Fact]
        public async Task Delete_Not_Existing_Project_Test()
        {
            ProjectController controller = await CreateController();

            var projectToDelete = new ProjectDto() { ProjectId = "NonExistentID" };

            var actionResult = await controller.DeleteProject(projectToDelete.ProjectId);

            //Assert  
            Assert.NotNull(actionResult);
            Assert.IsType<NotFoundObjectResult>(actionResult);
            var typedResult = actionResult as NotFoundObjectResult;
            Assert.NotNull(typedResult);
            Assert.Equal("Project not found!", typedResult!.Value);
            Assert.Equal(404, typedResult.StatusCode);
        }

        [Fact]
        public async Task Update_Existing_Project_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "UpdateTest");

            ProjectController controller = await CreateController(applicationDbContext);

            var projectToUpdate = new UpdateProjectDto
            {
                ProjectId = "ProjectId1",
                Name = "Project 42",
            };

            var actionResult = await controller.UpdateProject(projectToUpdate);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);

            var updatedOkResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(updatedOkResult);
            Assert.IsAssignableFrom<ProjectDto>(updatedOkResult!.Value);

            var updatedProjectTask = updatedOkResult.Value as ProjectDto;
            Assert.NotNull(updatedProjectTask);

            Assert.Equal(projectToUpdate.ProjectId, updatedProjectTask!.ProjectId);
            Assert.Equal(projectToUpdate.Name, updatedProjectTask!.Name);
        }

        [Fact]
        public async Task Update_Project_Another_User_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "UpdateTest");

            ProjectController controller = await CreateController(applicationDbContext, "ApplicationUser10");

            var projectToUpdate = new UpdateProjectDto
            {
                ProjectId = "ProjectId1",
                Name = "Project 42",
            };

            var actionResult = await controller.UpdateProject(projectToUpdate);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<NotFoundObjectResult>(actionResult.Result);

            var notFoundResult = actionResult.Result as NotFoundObjectResult;
            Assert.NotNull(notFoundResult);
            Assert.Equal("Project not found!", notFoundResult!.Value);
        }

        [Fact]
        public async Task Update_Not_Existing_Project_Test()
        {
            ProjectController controller = await CreateController();

            var projectToUpdate = new UpdateProjectDto
            {
                ProjectId = "NonExistingId",
                Name = "Project 42",
            };

            var actionResult = await controller.UpdateProject(projectToUpdate);

            // Assert
            Assert.NotNull(actionResult);
            Assert.IsType<NotFoundObjectResult>(actionResult.Result);

            var notFoundResult = actionResult.Result as NotFoundObjectResult;
            Assert.NotNull(notFoundResult);
            Assert.Equal(404, notFoundResult!.StatusCode);
            Assert.Equal("Project not found!", notFoundResult.Value);
        }

        [Fact]
        public async Task Create_New_Project_Test()
        {
            ProjectController controller = await CreateController();

            var projectToCreate = new CreateProjectDto
            {
                Name = "Project 420"
            };

            var actionResult = await controller.CreateProject(projectToCreate);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);

            var okActionResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okActionResult);

            Assert.IsAssignableFrom<ProjectDto>(okActionResult!.Value);
            var newlyCreatedProject = okActionResult.Value as ProjectDto;
            Assert.NotNull(newlyCreatedProject);

            Assert.Equal(projectToCreate.Name, newlyCreatedProject!.Name);
        }

        [Fact]
        public async Task Create_Project_With_Provided_Color_Persists_Color_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CreateColorProvided");
            ProjectController controller = await CreateController(applicationDbContext);

            var dto = new CreateProjectDto { Name = "With color", Color = "#ABCDEF" };

            var actionResult = await controller.CreateProject(dto);

            var okResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okResult);
            var created = okResult!.Value as ProjectDto;
            Assert.NotNull(created);
            Assert.Equal("#ABCDEF", created!.Color);

            // Confirm round-trip from DB.
            var saved = await applicationDbContext.Projects.FirstOrDefaultAsync(p => p.ProjectId == created.ProjectId);
            Assert.NotNull(saved);
            Assert.Equal("#ABCDEF", saved!.Color);
        }

        [Fact]
        public async Task Create_Project_Without_Color_Assigns_Palette_Default_Test()
        {
            // ApplicationUser1 has 3 active projects in seed (ProjectId1/4/5). The 4th project
            // should pick palette index 3 (= "#EC4899").
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CreateColorPaletteDefault");
            ProjectController controller = await CreateController(applicationDbContext);

            var actionResult = await controller.CreateProject(new CreateProjectDto { Name = "First default", Color = null });

            var okResult = actionResult.Result as OkObjectResult;
            var created = okResult!.Value as ProjectDto;
            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created!.Color));

            // Whatever palette index was picked, it must be one of the 5 palette colors.
            var palette = new[] { "#6366F1", "#F59E0B", "#10B981", "#EC4899", "#94A3B8" };
            Assert.Contains(created.Color, palette);

            // Existing-count = 3 → palette[3 % 5] = "#EC4899".
            Assert.Equal("#EC4899", created.Color);
        }

        [Fact]
        public async Task Create_Project_With_Empty_Color_Falls_Back_To_Palette_Test()
        {
            // Whitespace-only Color should be treated as missing.
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CreateColorEmpty");
            ProjectController controller = await CreateController(applicationDbContext);

            var actionResult = await controller.CreateProject(new CreateProjectDto { Name = "Empty color", Color = "   " });

            var okResult = actionResult.Result as OkObjectResult;
            var created = okResult!.Value as ProjectDto;
            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created!.Color));
        }

        [Fact]
        public async Task Create_Project_Round_Robin_Advances_Past_Soft_Deleted_Test()
        {
            // ApplicationUser1 has 3 active projects in seed (ProjectId1/4/5). The 4th
            // pickup is palette[3] (#EC4899). Soft-delete it then create another —
            // the index must advance to palette[4] (#94A3B8), not collide on
            // palette[3] again because the active count dropped back to 3.
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "RoundRobinPastDelete");
            ProjectController controller = await CreateController(applicationDbContext);

            var firstResult = await controller.CreateProject(new CreateProjectDto { Name = "Fourth" });
            var first = (firstResult.Result as OkObjectResult)!.Value as ProjectDto;
            Assert.Equal("#EC4899", first!.Color);

            await controller.DeleteProject(first.ProjectId);

            var secondResult = await controller.CreateProject(new CreateProjectDto { Name = "Fifth" });
            var second = (secondResult.Result as OkObjectResult)!.Value as ProjectDto;
            Assert.Equal("#94A3B8", second!.Color);
        }

        [Fact]
        public async Task Update_Project_Without_Color_Preserves_Saved_Color_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "UpdatePreservesColor");
            ProjectController controller = await CreateController(applicationDbContext);

            // Set an initial color via update.
            await controller.UpdateProject(new UpdateProjectDto
            {
                ProjectId = "ProjectId1",
                Name = "Project 1",
                Color = "#10B981"
            });

            // Now update only the name; Color omitted.
            var actionResult = await controller.UpdateProject(new UpdateProjectDto
            {
                ProjectId = "ProjectId1",
                Name = "Project 1 renamed",
                Color = null
            });

            var okResult = actionResult.Result as OkObjectResult;
            var updated = okResult!.Value as ProjectDto;
            Assert.NotNull(updated);
            Assert.Equal("Project 1 renamed", updated!.Name);
            Assert.Equal("#10B981", updated.Color);
        }

        [Fact]
        public async Task Delete_Project_Another_User_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "DeleteAuthTest");
            ProjectController controller = await CreateController(applicationDbContext, "ApplicationUser10");

            // Try to delete ApplicationUser1's project
            var actionResult = await controller.DeleteProject("ProjectId1");

            Assert.NotNull(actionResult);
            Assert.IsType<NotFoundObjectResult>(actionResult);
        }

        [Fact]
        public async Task Get_Project_Another_User_Returns_NotFound_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "GetAuthTest");
            ProjectController controller = await CreateController(applicationDbContext, "ApplicationUser10");

            var actionResult = await controller.GetProject("ProjectId1");

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<NotFoundObjectResult>(actionResult.Result);

            var notFoundResult = actionResult.Result as NotFoundObjectResult;
            Assert.NotNull(notFoundResult);
            Assert.Equal("Project not found!", notFoundResult!.Value);
        }

        [Fact]
        public async Task Search_Projects_By_Name()
        {
            ProjectController controller = await CreateController();
            var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

            var actionResult = await controller.SearchProjects(pagingParams, "Project 1", null);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;
            var projects = okResult!.Value as IEnumerable<ProjectDto>;
            Assert.NotNull(projects);
            Assert.Single(projects!);
            Assert.Equal("ProjectId1", projects!.First().ProjectId);
        }

        [Fact]
        public async Task Search_Projects_By_MinTaskCount()
        {
            ProjectController controller = await CreateController();
            var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

            var actionResult = await controller.SearchProjects(pagingParams, null, 1);

            Assert.NotNull(actionResult);
            var okResult = actionResult.Result as OkObjectResult;
            var projects = okResult!.Value as IEnumerable<ProjectDto>;
            Assert.NotNull(projects);
            Assert.Single(projects!);
            Assert.Equal("ProjectId1", projects!.First().ProjectId);
        }

        [Fact]
        public async Task Search_Projects_Another_User_Empty()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "SearchAuthTest");
            ProjectController controller = await CreateController(applicationDbContext, "NonExistentUser");
            var pagingParams = new PagingParameters { PageSize = 100, PageNumber = 1 };

            var actionResult = await controller.SearchProjects(pagingParams, null, null);

            Assert.NotNull(actionResult);
            var okResult = actionResult.Result as OkObjectResult;
            var projects = okResult!.Value as IEnumerable<ProjectDto>;
            Assert.NotNull(projects);
            Assert.Empty(projects!);
        }

        [Fact]
        public async Task Delete_Project_Soft_Deletes_Project_And_Cascades_To_Tasks_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CascadeDelete");
            ProjectController controller = await CreateController(applicationDbContext);

            var actionResult = await controller.DeleteProject("ProjectId1");
            Assert.IsType<NoContentResult>(actionResult);

            var project = await applicationDbContext.Projects.IgnoreQueryFilters().FirstAsync(p => p.ProjectId == "ProjectId1");
            Assert.NotNull(project.DeletedAt);

            var tasks = await applicationDbContext.TrackedTasks.IgnoreQueryFilters()
                .Where(t => t.ProjectId == "ProjectId1")
                .ToListAsync();

            Assert.Equal(3, tasks.Count);
            Assert.All(tasks, t => Assert.Equal(project.DeletedAt, t.DeletedAt));
        }

        [Fact]
        public async Task Restore_Project_Restores_Cascaded_Tasks_Only_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "CascadeRestore");
            ProjectController controller = await CreateController(applicationDbContext);

            var taskConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["TrashRetention:Days"] = "30" })
                .Build();
            var taskController = new TrackedTaskController(
                new RepositoryFactory(applicationDbContext),
                _mapper,
                new Mock<ILogger<TrackedTaskController>>().Object,
                taskConfig)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "ApplicationUser1") }))
                    }
                }
            };

            await taskController.DeleteTrackedTask("TrackedTaskId1");
            await controller.DeleteProject("ProjectId1");

            var actionResult = await controller.RestoreProject("ProjectId1");
            Assert.IsType<NoContentResult>(actionResult);

            var task2 = await applicationDbContext.TrackedTasks.FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId2");
            var task3 = await applicationDbContext.TrackedTasks.FirstOrDefaultAsync(t => t.TaskId == "TrackedTaskId3");
            Assert.NotNull(task2);
            Assert.NotNull(task3);

            var task1 = await applicationDbContext.TrackedTasks.IgnoreQueryFilters().FirstAsync(t => t.TaskId == "TrackedTaskId1");
            Assert.NotNull(task1.DeletedAt);

            var project = await applicationDbContext.Projects.FirstOrDefaultAsync(p => p.ProjectId == "ProjectId1");
            Assert.NotNull(project);
        }

        [Fact]
        public async Task GetTrash_Projects_Returns_Only_Deleted_Owned_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "TrashProjects");
            ProjectController controller = await CreateController(applicationDbContext);

            await controller.DeleteProject("ProjectId4");
            await controller.DeleteProject("ProjectId5");

            var actionResult = await controller.GetTrashProjects();
            var okResult = actionResult.Result as OkObjectResult;
            var items = (okResult!.Value as IEnumerable<TrashItemDto>)!.ToList();

            Assert.Equal(2, items.Count);
            Assert.All(items, i => Assert.InRange(i.DaysRemaining, 29, 30));
        }

        [Fact]
        public async Task Purge_Project_Hard_Removes_Project_Row_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "PurgeProject");
            ProjectController controller = await CreateController(applicationDbContext);

            await controller.DeleteProject("ProjectId1");

            var actionResult = await controller.PurgeProject("ProjectId1");
            Assert.IsType<NoContentResult>(actionResult);

            var project = await applicationDbContext.Projects.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.ProjectId == "ProjectId1");
            Assert.Null(project);

            var taskCount = await applicationDbContext.TrackedTasks.IgnoreQueryFilters()
                .CountAsync(t => t.TaskId == "TrackedTaskId1" || t.TaskId == "TrackedTaskId2" || t.TaskId == "TrackedTaskId3");
            Assert.Equal(3, taskCount);
        }

        [Fact]
        public async Task Purge_Project_Another_User_Returns_NotFound_Test()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "PurgeProjectAuth");
            ProjectController owner = await CreateController(applicationDbContext);
            await owner.DeleteProject("ProjectId1");

            ProjectController other = await CreateController(applicationDbContext, "ApplicationUser10");
            var actionResult = await other.PurgeProject("ProjectId1");

            Assert.IsType<NotFoundObjectResult>(actionResult);
        }

        protected override async Task<ProjectController> CreateController(ApplicationDbContext? applicationDbContext = null, string userId = "ApplicationUser1")
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

            ProjectController controller = new(repositoryFactory, _mapper, _loggerMock.Object, configuration)
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
