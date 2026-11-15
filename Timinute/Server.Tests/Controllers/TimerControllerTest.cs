using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Timinute.Server.Controllers;
using Timinute.Server.Helpers;
using Timinute.Server.Services.App;
using Timinute.Shared.Dtos.Timer;
using Timinute.Shared.Dtos.TrackedTask;
using Xunit;

namespace Timinute.Server.Tests.Controllers
{
    // Status-code mapping only; behaviour lives in TimerAppServiceSqliteTest.
    public class TimerControllerTest
    {
        private const string UserId = "ApplicationUser1";

        private static TimerController CreateController(Mock<ITimerAppService> service, string? userId = UserId)
        {
            var claims = userId == null ? Array.Empty<Claim>() : new[] { new Claim(Constants.Claims.UserId, userId) };
            return new TimerController(service.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
                }
            };
        }

        private static int? StatusOf(IActionResult result) => result switch
        {
            ObjectResult o => o.StatusCode ?? 200,
            StatusCodeResult s => s.StatusCode,
            _ => null,
        };

        [Fact]
        public async Task Get_NoTimer_Returns204()
        {
            var service = new Mock<ITimerAppService>();
            var result = await CreateController(service).Get();
            Assert.IsType<NoContentResult>(result.Result);
        }

        [Fact]
        public async Task Get_Running_Returns200()
        {
            var service = new Mock<ITimerAppService>();
            service.Setup(s => s.GetAsync(UserId)).ReturnsAsync(new RunningTimerDto { Name = "x", StartDate = DateTimeOffset.UtcNow });
            var result = await CreateController(service).Get();
            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async Task Get_NoUserClaim_Returns401()
        {
            var result = await CreateController(new Mock<ITimerAppService>(), userId: null).Get();
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task Start_Returns201()
        {
            var service = new Mock<ITimerAppService>();
            service.Setup(s => s.StartAsync(UserId, It.IsAny<StartTimerDto>())).ReturnsAsync(new RunningTimerDto { Name = "x" });
            var result = await CreateController(service).Start(new StartTimerDto { Name = "xx" });
            Assert.Equal(201, StatusOf(result.Result!));
        }

        [Theory]
        [InlineData(typeof(TimerConflictException), 409)]
        [InlineData(typeof(ProjectOwnershipException), 404)]
        public async Task Start_Maps_Exceptions(Type exceptionType, int expected)
        {
            var service = new Mock<ITimerAppService>();
            service.Setup(s => s.StartAsync(UserId, It.IsAny<StartTimerDto>())).ThrowsAsync((Exception)Activator.CreateInstance(exceptionType)!);
            var result = await CreateController(service).Start(new StartTimerDto { Name = "xx" });
            Assert.Equal(expected, StatusOf(result.Result!));
        }

        [Fact]
        public async Task Start_ValidationFailure_Returns422()
        {
            var service = new Mock<ITimerAppService>();
            service.Setup(s => s.StartAsync(UserId, It.IsAny<StartTimerDto>())).ThrowsAsync(new AppValidationException("bad"));
            var result = await CreateController(service).Start(new StartTimerDto { Name = "x" });
            Assert.Equal(422, StatusOf(result.Result!));
        }

        [Fact]
        public async Task Stop_Returns200()
        {
            var service = new Mock<ITimerAppService>();
            service.Setup(s => s.StopAsync(UserId, It.IsAny<StopTimerDto>()))
                   .ReturnsAsync(new StopTimerResultDto { TrackedTask = new TrackedTaskDto() });
            var result = await CreateController(service).Stop(null);
            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async Task Stop_NoTimer_Returns404()
        {
            var service = new Mock<ITimerAppService>();
            service.Setup(s => s.StopAsync(UserId, It.IsAny<StopTimerDto>())).ThrowsAsync(new TimerNotFoundException());
            var result = await CreateController(service).Stop(new StopTimerDto());
            Assert.Equal(404, StatusOf(result.Result!));
        }

        [Fact]
        public async Task Stop_BadEndAt_Returns422()
        {
            var service = new Mock<ITimerAppService>();
            service.Setup(s => s.StopAsync(UserId, It.IsAny<StopTimerDto>())).ThrowsAsync(new InvalidTimeRangeException("bad"));
            var result = await CreateController(service).Stop(new StopTimerDto());
            Assert.Equal(422, StatusOf(result.Result!));
        }

        [Fact]
        public async Task Discard_Returns204_Or404()
        {
            var ok = new Mock<ITimerAppService>();
            Assert.IsType<NoContentResult>(await CreateController(ok).Discard());

            var missing = new Mock<ITimerAppService>();
            missing.Setup(s => s.DiscardAsync(UserId)).ThrowsAsync(new TimerNotFoundException());
            Assert.Equal(404, StatusOf(await CreateController(missing).Discard()));
        }
    }
}
