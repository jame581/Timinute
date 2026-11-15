using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Blazored.SessionStorage;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;
using Moq;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Client.Services;
using Timinute.Client.Tests.Helpers;
using Timinute.Shared.Dtos;
using Timinute.Shared.Dtos.Timer;
using Timinute.Shared.Dtos.TrackedTask;
using Xunit;

namespace Timinute.Client.Tests.Services
{
    public class TimerNotificationServiceTest
    {
        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
        private static readonly DateTimeOffset Start = new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

        private readonly FakeTimeProvider clock = new(Start);
        private readonly FakeNotificationInterop interop = new();
        private readonly List<HttpRequestMessage> requests = new();
        private readonly TimerService timer;
        private readonly TimerNotificationService service;
        private readonly UserProfileService profile;

        private RunningTimerDto? running = new() { Name = "API review", StartDate = Start };
        private UserPreferencesDto prefs = new() { LongTimerWarningHours = 8.0m, IdleWarningMinutes = 15 };

        public TimerNotificationServiceTest()
        {
            var handler = new StubHttpMessageHandler(req =>
            {
                requests.Add(req);
                var path = req.RequestUri!.AbsolutePath;
                HttpResponseMessage response = path switch
                {
                    "/User/me" => Json(new UserProfileDto { Preferences = prefs }),
                    "/Timer" when running == null => new HttpResponseMessage(HttpStatusCode.NoContent),
                    "/Timer" => Json(running),
                    "/Timer/stop" => Json(new StopTimerResultDto { TrackedTask = new TrackedTaskDto { Name = "API review" } }),
                    _ => new HttpResponseMessage(HttpStatusCode.NotFound),
                };
                return Task.FromResult(response);
            });
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(Constants.API.ClientName))
                   .Returns(() => new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

            timer = new TimerService(factory.Object, new Mock<ISessionStorageService>().Object, new Mock<IJSRuntime>().Object, clock);
            profile = new UserProfileService(factory.Object);
            service = new TimerNotificationService(timer, profile, interop, clock);
        }

        private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: WebJson) };

        private async Task At(TimeSpan elapsed)
        {
            clock.SetUtcNow(Start + elapsed);
            await timer.RefreshAsync();
            await service.EvaluateAsync();
        }

        private static string Iso(DateTimeOffset t) => t.UtcDateTime.ToString("o");

        [Fact]
        public async Task NoTimer_NoItems()
        {
            running = null;
            await At(TimeSpan.FromHours(9));

            Assert.Empty(service.Items);
        }

        [Fact]
        public async Task Elapsed_Below_Threshold_NoItem()
        {
            await At(TimeSpan.FromHours(7.9));

            Assert.Empty(service.Items);
        }

        [Fact]
        public async Task Elapsed_At_8h_Raises_LongRunning()
        {
            await At(TimeSpan.FromHours(8));

            var item = Assert.Single(service.Items);
            Assert.Equal(BellRule.LongRunning, item.Rule);
            Assert.Equal("Timer running for 8h 0m", item.Title);
            Assert.Equal("API review", item.Detail);
            Assert.Equal(Start, item.TimerStart);
        }

        [Fact]
        public async Task Threshold_Zero_Disables_LongRunning()
        {
            prefs.LongTimerWarningHours = 0;
            await At(TimeSpan.FromHours(30));

            Assert.Empty(service.Items);
        }

        [Fact]
        public async Task Dismiss_At_8h_Suppresses_Until_16h()
        {
            await At(TimeSpan.FromHours(8));
            service.Dismiss(service.Items.Single().Id);

            await At(TimeSpan.FromHours(15) + TimeSpan.FromMinutes(59));
            Assert.Empty(service.Items);

            await At(TimeSpan.FromHours(16));
            Assert.Single(service.Items);
        }

        [Fact]
        public async Task NewTimer_Resets_Dismiss_Memory()
        {
            await At(TimeSpan.FromHours(8));
            service.Dismiss(service.Items.Single().Id);

            running = new RunningTimerDto { Name = "Next", StartDate = Start.AddHours(9) };
            await At(TimeSpan.FromHours(17));

            var item = Assert.Single(service.Items);
            Assert.Equal(Start.AddHours(9), item.TimerStart);
        }

        [Fact]
        public async Task Changing_Threshold_Forgets_Old_Dismissal()
        {
            prefs.LongTimerWarningHours = 2.0m;
            await At(TimeSpan.FromHours(8)); // 4th multiple of 2 h, dismissed below
            service.Dismiss(service.Items.Single().Id);

            prefs.LongTimerWarningHours = 4.0m;
            await profile.InvalidateAsync();
            await At(TimeSpan.FromHours(9));

            Assert.Single(service.Items);
        }

        [Fact]
        public async Task Turning_Idle_Off_Clears_Idle_Items()
        {
            await At(TimeSpan.FromHours(1));
            await service.OnIdleEnded(Iso(Start.AddMinutes(20)), Iso(Start.AddMinutes(50)));
            Assert.Single(service.Items);

            prefs.IdleWarningMinutes = 0;
            await profile.InvalidateAsync();
            await At(TimeSpan.FromHours(1.1));

            Assert.Empty(service.Items);
        }

        [Fact]
        public async Task IdleEnded_Above_Threshold_Raises_Idle_With_Times()
        {
            await At(TimeSpan.FromHours(1));

            await service.OnIdleEnded(Iso(Start.AddMinutes(20)), Iso(Start.AddMinutes(50)));

            var item = Assert.Single(service.Items);
            Assert.Equal(BellRule.Idle, item.Rule);
            Assert.Equal(Start.AddMinutes(20), item.IdleStart);
            Assert.Equal(Start.AddMinutes(50), item.IdleEnd);
        }

        [Fact]
        public async Task IdleEnded_Below_Threshold_Ignored()
        {
            await At(TimeSpan.FromHours(1));

            await service.OnIdleEnded(Iso(Start.AddMinutes(20)), Iso(Start.AddMinutes(34)));

            Assert.Empty(service.Items);
        }

        [Fact]
        public async Task Idle_Before_Timer_Start_Is_Clamped_To_Start()
        {
            await At(TimeSpan.FromHours(1));

            await service.OnIdleEnded(Iso(Start.AddMinutes(-30)), Iso(Start.AddMinutes(20)));

            Assert.Equal(Start, service.Items.Single().IdleStart);
        }

        [Fact]
        public async Task Idle_Zero_Disables()
        {
            prefs.IdleWarningMinutes = 0;
            await At(TimeSpan.FromHours(1));

            await service.OnIdleEnded(Iso(Start.AddMinutes(5)), Iso(Start.AddMinutes(50)));

            Assert.Empty(service.Items);
            Assert.False(interop.IdleWatching);
        }

        [Fact]
        public async Task IdleWatch_Runs_Only_While_A_Timer_Runs()
        {
            await At(TimeSpan.FromMinutes(5));
            Assert.True(interop.IdleWatching);
            Assert.Equal(15, interop.IdleMinutes);

            running = null;
            await At(TimeSpan.FromMinutes(6));
            Assert.False(interop.IdleWatching);
        }

        [Fact]
        public async Task StopAtIdleStart_Calls_Stop_With_IdleStart()
        {
            await At(TimeSpan.FromHours(1));
            await service.OnIdleEnded(Iso(Start.AddMinutes(20)), Iso(Start.AddMinutes(50)));

            running = null; // the server has no timer once stopped
            await service.StopAtIdleStartAsync(service.Items.Single().Id);

            var stop = requests.Last(r => r.RequestUri!.AbsolutePath == "/Timer/stop");
            var body = await stop.Content!.ReadFromJsonAsync<StopTimerDto>(WebJson);
            Assert.Equal(Start.AddMinutes(20), body!.EndAt);
            Assert.Empty(service.Items);
        }

        [Fact]
        public async Task StopNow_Stops_Without_EndAt_And_Clears()
        {
            await At(TimeSpan.FromHours(8));

            running = null;
            await service.StopNowAsync();

            var stop = requests.Last(r => r.RequestUri!.AbsolutePath == "/Timer/stop");
            var body = await stop.Content!.ReadFromJsonAsync<StopTimerDto>(WebJson);
            Assert.Null(body!.EndAt);
            Assert.Empty(service.Items);
        }

        [Fact]
        public async Task KeepIdle_Removes_Item_New_Period_Raises_Again()
        {
            await At(TimeSpan.FromHours(1));
            await service.OnIdleEnded(Iso(Start.AddMinutes(10)), Iso(Start.AddMinutes(30)));
            service.KeepIdle(service.Items.Single().Id);
            Assert.Empty(service.Items);

            await service.OnIdleEnded(Iso(Start.AddMinutes(35)), Iso(Start.AddMinutes(55)));

            Assert.Equal(Start.AddMinutes(35), service.Items.Single().IdleStart);
        }

        [Fact]
        public async Task BrowserEnabled_And_Granted_Shows_Notification_With_Tag()
        {
            interop.Permission = "granted";
            Assert.True(await service.SetBrowserEnabledAsync(true));

            await At(TimeSpan.FromHours(8));
            await At(TimeSpan.FromHours(8.2)); // same warning again: no second notification

            var shown = Assert.Single(interop.Shown);
            Assert.Equal($"LongRunning:{Start.UtcTicks}", shown.Tag);
            Assert.True(interop.OptIn);
        }

        [Fact]
        public async Task Stopping_Timer_Forgets_Shown_Notifications()
        {
            interop.Permission = "granted";
            await service.SetBrowserEnabledAsync(true);
            await At(TimeSpan.FromHours(8));
            Assert.Equal(1, service.NotifiedCount);

            running = null;
            await At(TimeSpan.FromHours(8.1));

            Assert.Equal(0, service.NotifiedCount);
        }

        [Fact]
        public async Task BrowserDisabled_NoJsShow()
        {
            await At(TimeSpan.FromHours(8));

            Assert.Single(service.Items);
            Assert.Empty(interop.Shown);
        }

        [Fact]
        public async Task BrowserToggle_PermissionDenied_Returns_False_And_Stays_Off()
        {
            interop.Permission = "default";
            interop.RequestResult = "denied";

            Assert.False(await service.SetBrowserEnabledAsync(true));

            Assert.False(service.BrowserEnabled);
            Assert.False(interop.OptIn);
        }

        [Fact]
        public async Task TimerStopped_ClearsItems()
        {
            await At(TimeSpan.FromHours(8));
            Assert.Single(service.Items);

            running = null;
            await At(TimeSpan.FromHours(8.1));

            Assert.Empty(service.Items);
        }

        [Fact]
        public async Task Initialize_Reads_OptIn_And_Polls_Every_30s()
        {
            interop.OptIn = true;
            await timer.RefreshAsync();
            await service.InitializeAsync();
            Assert.True(service.BrowserEnabled);

            var raised = 0;
            service.Changed += () => raised++;
            clock.Advance(TimeSpan.FromSeconds(30));
            // The tick's evaluation runs on the thread pool; wait for it rather than a fixed delay.
            for (var i = 0; i < 100 && raised == 0; i++) await Task.Delay(20);

            Assert.True(raised > 0);
        }
    }
}
