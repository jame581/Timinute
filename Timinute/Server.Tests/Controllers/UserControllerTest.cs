using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Timinute.Server;
using Timinute.Server.Controllers;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Server.Tests.Helpers;
using Timinute.Shared.Dtos;
using Xunit;

namespace Timinute.Server.Tests.Controllers
{
    public class UserControllerTest : ControllerTestBase<UserController>
    {
        private const string _databaseName = "UserController_Test_DB";
        private readonly IMapper _mapper;

        public UserControllerTest()
        {
            var configExpression = new MapperConfigurationExpression();
            configExpression.AddProfile<MappingProfile>();
            var configuration = new MapperConfiguration(configExpression, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            _mapper = new Mapper(configuration);
        }

        [Fact]
        public async Task Get_Me_Returns_Profile_With_Aggregated_Totals_Test()
        {
            // ApplicationUser1 has 3 active projects (1, 4, 5) and 4 active tracked tasks
            // (TrackedTaskId1..4) totaling 2+4+4+4 = 14 hours per the seed.
            var applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "Happy");
            var user = new ApplicationUser
            {
                Id = "ApplicationUser1",
                Email = "test1@email.com",
                FirstName = "Jan",
                LastName = "Testovic",
                CreatedAt = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
            };

            var controller = CreateControllerWith(applicationDbContext, user);

            var actionResult = await controller.GetMe();

            var okResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okResult);
            var profile = okResult!.Value as UserProfileDto;
            Assert.NotNull(profile);

            Assert.Equal("Jan", profile!.FirstName);
            Assert.Equal("Testovic", profile.LastName);
            Assert.Equal("test1@email.com", profile.Email);
            Assert.Equal(user.CreatedAt, profile.CreatedAt);
            Assert.Equal(3, profile.ProjectCount);
            Assert.Equal(4, profile.TaskCount);
            Assert.Equal(TimeSpan.FromHours(14), profile.TotalTrackedTime);
        }

        [Fact]
        public async Task Get_Me_Returns_Preferences_With_Defaults_Test()
        {
            var applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "Prefs");
            var user = new ApplicationUser
            {
                Id = "ApplicationUser1",
                Email = "test1@email.com",
                FirstName = "Jan",
                LastName = "Testovic",
                CreatedAt = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
                // Preferences not explicitly set — relies on the C# initializer's default new() for parity with the GetMe response.
            };

            var controller = CreateControllerWith(applicationDbContext, user);

            var actionResult = await controller.GetMe();

            var okResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okResult);
            var profile = okResult!.Value as UserProfileDto;
            Assert.NotNull(profile);

            Assert.NotNull(profile!.Preferences);
            Assert.Equal(ThemePreference.System, profile.Preferences.Theme);
            Assert.Equal(32.0m, profile.Preferences.WeeklyGoalHours);
            Assert.Equal(8.0m, profile.Preferences.WorkdayHoursPerDay);
        }

        [Fact]
        public async Task Get_Me_Without_Sub_Claim_Returns_Unauthorized_Test()
        {
            var applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "NoSub");
            var userManagerMock = BuildUserManagerMock();

            var controller = new UserController(userManagerMock.Object, _mapper, new RepositoryFactory(applicationDbContext))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
                }
            };

            var actionResult = await controller.GetMe();

            Assert.IsType<UnauthorizedResult>(actionResult.Result);
        }

        [Fact]
        public async Task Get_Me_When_User_Not_Found_Returns_NotFound_Test()
        {
            var applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "Missing");

            var userManagerMock = BuildUserManagerMock();
            userManagerMock.Setup(m => m.FindByIdAsync("ApplicationUserMissing"))
                .ReturnsAsync((ApplicationUser?)null);

            var controller = new UserController(userManagerMock.Object, _mapper, new RepositoryFactory(applicationDbContext))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "ApplicationUserMissing") }))
                    }
                }
            };

            var actionResult = await controller.GetMe();

            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        protected override Task<UserController> CreateController(ApplicationDbContext? applicationDbContext = null, string userId = "ApplicationUser1")
        {
            // Not used directly — the suite builds controllers via CreateControllerWith because we need a stubbed UserManager.
            throw new NotImplementedException("Use CreateControllerWith for UserControllerTest.");
        }

        private static Mock<UserManager<ApplicationUser>> BuildUserManagerMock()
        {
            return new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!, null!, null!, null!, null!, null!, null!, null!);
        }

        private UserController CreateControllerWith(ApplicationDbContext db, ApplicationUser user)
        {
            var userManagerMock = BuildUserManagerMock();
            userManagerMock.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);

            return new UserController(userManagerMock.Object, _mapper, new RepositoryFactory(db))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim("sub", user.Id),
                            new Claim(ClaimTypes.Name, user.Email!)
                        }))
                    }
                }
            };
        }
    }
}
