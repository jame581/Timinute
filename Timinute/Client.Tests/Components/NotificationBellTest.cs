using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Blazored.SessionStorage;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;
using Moq;
using Timinute.Client.Helpers;
using Timinute.Client.Services;
using Timinute.Client.Shared;
using Timinute.Client.Tests.Helpers;
using Timinute.Shared.Dtos;
using Timinute.Shared.Dtos.Timer;
using Timinute.Shared.Dtos.TrackedTask;
using Xunit;

namespace Timinute.Client.Tests.Components
{
    public class NotificationBellTest : BunitContext
    {
        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
        private static readonly DateTimeOffset Start = new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

        private readonly FakeTimeProvider clock = new(Start);
        private readonly List<HttpRequestMessage> requests = new();
        private readonly TimerService timer;
        private readonly TimerNotificationService notifications;

        private RunningTimerDto? running = new() { Name = "API review", StartDate = Start };

        public NotificationBellTest()
        {
            JSInterop.Mode = JSRuntimeMode.Loose;

            var handler = new StubHttpMessageHandler(req =>
            {
                requests.Add(req);
                HttpResponseMessage response = req.RequestUri!.AbsolutePath switch
                {
                    "/User/me" => Json(new UserProfileDto { Preferences = new UserPreferencesDto { LongTimerWarningHours = 8.0m, IdleWarningMinutes = 15 } }),
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
            notifications = new TimerNotificationService(timer, new UserProfileService(factory.Object), new FakeNotificationInterop(), clock);

            Services.AddSingleton(notifications);
            Services.AddSingleton(new Radzen.NotificationService());
        }

        private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: WebJson) };

        private async Task At(TimeSpan elapsed)
        {
            clock.SetUtcNow(Start + elapsed);
            await timer.RefreshAsync();
            await notifications.EvaluateAsync();
        }

        private static string Iso(DateTimeOffset t) => t.UtcDateTime.ToString("o");

        [Fact]
        public async Task Empty_Shows_No_Badge_And_Empty_State()
        {
            await At(TimeSpan.FromHours(1));
            var cut = Render<NotificationBell>();

            Assert.Empty(cut.FindAll(".aurora-bell__badge"));
            var bell = cut.Find(".aurora-bell__btn");
            Assert.Equal("false", bell.GetAttribute("aria-expanded"));

            bell.Click();

            Assert.Equal("true", cut.Find(".aurora-bell__btn").GetAttribute("aria-expanded"));
            Assert.Equal("Nothing needs your attention", cut.Find(".aurora-bell__empty").TextContent);
        }

        [Fact]
        public async Task LongRunning_Shows_Badge_And_Item_In_List()
        {
            var cut = Render<NotificationBell>();
            await At(TimeSpan.FromHours(8));

            cut.WaitForAssertion(() => Assert.Equal("1", cut.Find(".aurora-bell__badge").TextContent));
            Assert.Equal("Notifications, 1 item", cut.Find(".aurora-bell__btn").GetAttribute("aria-label"));

            cut.Find(".aurora-bell__btn").Click();

            var list = cut.Find(".aurora-bell__list");
            Assert.Equal("list", list.GetAttribute("role"));
            Assert.Equal("Timer running for 8h 0m", cut.Find(".aurora-bell__item-title").TextContent);
            Assert.Equal("API review", cut.Find(".aurora-bell__item-detail").TextContent);
            var labels = cut.FindAll(".aurora-bell__action").Select(b => b.TextContent.Trim()).ToArray();
            Assert.Equal(new[] { "Stop now", "Open tracker", "Dismiss" }, labels);
        }

        [Fact]
        public async Task StopNow_Stops_Timer_And_Shows_Empty_State()
        {
            await At(TimeSpan.FromHours(8));
            var cut = Render<NotificationBell>();
            cut.Find(".aurora-bell__btn").Click();

            running = null;
            cut.FindAll(".aurora-bell__action").Single(b => b.TextContent.Trim() == "Stop now").Click();

            cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".aurora-bell__empty")));
            Assert.Contains(requests, r => r.RequestUri!.AbsolutePath == "/Timer/stop");
            Assert.Empty(cut.FindAll(".aurora-bell__badge"));
        }

        [Fact]
        public async Task Dismiss_Removes_Item()
        {
            await At(TimeSpan.FromHours(8));
            var cut = Render<NotificationBell>();
            cut.Find(".aurora-bell__btn").Click();

            cut.FindAll(".aurora-bell__action").Single(b => b.TextContent.Trim() == "Dismiss").Click();

            Assert.NotNull(cut.Find(".aurora-bell__empty"));
            Assert.Empty(notifications.Items);
            Assert.DoesNotContain(requests, r => r.RequestUri!.AbsolutePath == "/Timer/stop");
        }

        [Fact]
        public async Task OpenTracker_Navigates_And_Closes()
        {
            await At(TimeSpan.FromHours(8));
            var cut = Render<NotificationBell>();
            cut.Find(".aurora-bell__btn").Click();

            cut.FindAll(".aurora-bell__action").Single(b => b.TextContent.Trim() == "Open tracker").Click();

            Assert.EndsWith("/timetracker", Services.GetRequiredService<NavigationManager>().Uri);
            Assert.Empty(cut.FindAll(".aurora-bell__panel"));
        }

        [Fact]
        public async Task Idle_Item_Offers_Stop_At_Local_Time_And_Keep()
        {
            await At(TimeSpan.FromHours(1));
            await notifications.OnIdleEnded(Iso(Start.AddMinutes(20)), Iso(Start.AddMinutes(50)));
            var cut = Render<NotificationBell>();
            cut.Find(".aurora-bell__btn").Click();

            var expected = "Stop at " + Start.AddMinutes(20).ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            var labels = cut.FindAll(".aurora-bell__action").Select(b => b.TextContent.Trim()).ToArray();
            Assert.Equal(new[] { expected, "Keep" }, labels);

            cut.FindAll(".aurora-bell__action").Single(b => b.TextContent.Trim() == "Keep").Click();
            Assert.Empty(notifications.Items);
        }

        [Fact]
        public async Task StopAtIdle_Sends_EndAt_Idle_Start()
        {
            await At(TimeSpan.FromHours(1));
            await notifications.OnIdleEnded(Iso(Start.AddMinutes(20)), Iso(Start.AddMinutes(50)));
            var cut = Render<NotificationBell>();
            cut.Find(".aurora-bell__btn").Click();

            running = null;
            cut.FindAll(".aurora-bell__action").First(b => b.TextContent.Trim().StartsWith("Stop at")).Click();

            cut.WaitForAssertion(() => Assert.Contains(requests, r => r.RequestUri!.AbsolutePath == "/Timer/stop"));
            var body = await requests.Last(r => r.RequestUri!.AbsolutePath == "/Timer/stop").Content!.ReadFromJsonAsync<StopTimerDto>(WebJson);
            Assert.Equal(Start.AddMinutes(20), body!.EndAt);
        }

        [Fact]
        public void Escape_Closes_Panel()
        {
            var cut = Render<NotificationBell>();
            cut.Find(".aurora-bell__btn").Click();
            Assert.NotNull(cut.Find(".aurora-bell__panel"));

            cut.Find(".aurora-bell").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

            Assert.Empty(cut.FindAll(".aurora-bell__panel"));
            Assert.Equal("false", cut.Find(".aurora-bell__btn").GetAttribute("aria-expanded"));
        }

        [Fact]
        public void Outside_Click_Closes_Panel()
        {
            var cut = Render<NotificationBell>();
            cut.Find(".aurora-bell__btn").Click();

            cut.Find(".aurora-bell__scrim").Click();

            Assert.Empty(cut.FindAll(".aurora-bell__panel"));
        }

        [Fact]
        public void Navigation_Closes_Panel()
        {
            var cut = Render<NotificationBell>();
            cut.Find(".aurora-bell__btn").Click();

            Services.GetRequiredService<NavigationManager>().NavigateTo("projectmanager");

            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".aurora-bell__panel")));
        }

        [Fact]
        public async Task Dismiss_Keeps_Focus_In_Panel()
        {
            await At(TimeSpan.FromHours(8));
            var cut = Render<NotificationBell>();
            cut.Find(".aurora-bell__btn").Click();
            var before = JSInterop.Invocations.Count(i => i.Identifier.EndsWith("focus"));

            cut.FindAll(".aurora-bell__action").Single(b => b.TextContent.Trim() == "Dismiss").Click();

            Assert.True(JSInterop.Invocations.Count(i => i.Identifier.EndsWith("focus")) > before);
            Assert.NotNull(cut.Find(".aurora-bell__panel"));
        }

        [Fact]
        public void Without_Trigger_Panel_Is_Modal_And_Returns_Focus()
        {
            var returned = false;
            var cut = Render<NotificationBell>(p => p
                .Add(b => b.ShowTrigger, false)
                .Add(b => b.ReturnFocus, () => { returned = true; return ValueTask.CompletedTask; }));
            notifications.RequestOpen();
            cut.WaitForAssertion(() => Assert.Equal("true", cut.Find(".aurora-bell__panel").GetAttribute("aria-modal")));
            Assert.Equal("dialog", cut.Find(".aurora-bell__panel").GetAttribute("role"));

            cut.Find(".aurora-bell__scrim").Click();

            Assert.True(returned);
        }

        [Fact]
        public void Without_Trigger_RequestOpen_Opens_Panel()
        {
            var cut = Render<NotificationBell>(p => p.Add(b => b.ShowTrigger, false));
            Assert.Empty(cut.FindAll(".aurora-bell__btn"));
            Assert.Empty(cut.FindAll(".aurora-bell__panel"));

            notifications.RequestOpen();

            cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".aurora-bell__panel")));
        }
    }
}
