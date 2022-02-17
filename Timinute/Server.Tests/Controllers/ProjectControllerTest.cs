using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Timinute.Server.Controllers;
using Timinute.Server.Data;
using Timinute.Server.Models.Paging;
using Timinute.Server.Repository;
using Timinute.Server.Tests.Helpers;
using Timinute.Shared.Dtos.Project;
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
            var myProfile = new MappingProfile();
            var configuration = new MapperConfiguration(cfg => cfg.AddProfile(myProfile));
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
            Assert.IsAssignableFrom<UnauthorizedResult>(actionResult.Result);

            var unauthorizedResult = actionResult.Result as UnauthorizedResult;
            Assert.NotNull(unauthorizedResult);

            Assert.Equal((int)HttpStatusCode.Unauthorized, unauthorizedResult.StatusCode);
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

            ProjectController controller = new(repositoryFactory, _mapper, _loggerMock.Object)
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
