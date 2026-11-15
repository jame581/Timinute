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
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;
using Moq;
using Timinute.Client.Helpers;
using Timinute.Client.Pages;
using Timinute.Client.Services;
using Timinute.Client.Tests.Helpers;
using Timinute.Shared.Dtos;
using Xunit;

namespace Timinute.Client.Tests.Components
{
    public class ProfileTimerPreferencesTest : BunitContext
    {
        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

        private readonly FakeNotificationInterop interop = new();
        private readonly List<(HttpRequestMessage Request, string? Body)> requests = new();
        private readonly UserPreferencesDto prefs = new() { LongTimerWarningHours = 2.5m, IdleWarningMinutes = 0 };

        public ProfileTimerPreferencesTest()
        {
            JSInterop.Mode = JSRuntimeMode.Loose;

            var handler = new StubHttpMessageHandler(async req =>
            {
                requests.Add((req, req.Content == null ? null : await req.Content.ReadAsStringAsync()));
                return req.RequestUri!.AbsolutePath switch
                {
                    "/User/me" when req.Method == HttpMethod.Get => Json(new UserProfileDto { FirstName = "Ada", Email = "ada@example.com", Preferences = prefs }),
                    "/User/me/preferences" => Json(prefs),
                    "/Timer" => new HttpResponseMessage(HttpStatusCode.NoContent),
                    _ => new HttpResponseMessage(HttpStatusCode.NotFound),
                };
            });
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(Constants.API.ClientName))
                   .Returns(() => new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 14, 8, 0, 0, TimeSpan.Zero));
            var profile = new UserProfileService(factory.Object);
            var timer = new TimerService(factory.Object, new Mock<ISessionStorageService>().Object, new Mock<IJSRuntime>().Object, clock);

            Services.AddSingleton(factory.Object);
            Services.AddSingleton(profile);
            Services.AddSingleton(new ThemeService(JSInterop.JSRuntime, factory.Object, profile));
            Services.AddSingleton(new TimerNotificationService(timer, profile, interop, clock));
            Services.AddSingleton(new Radzen.NotificationService());
#pragma warning disable CS0618 // still what Profile injects for sign-out
            Services.AddSingleton(new SignOutSessionStateManager(JSInterop.JSRuntime));
#pragma warning restore CS0618
        }

        private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: WebJson) };

        [Fact]
        public void Loads_Timer_Warning_Values()
        {
            var cut = Render<Profile>();

            cut.WaitForAssertion(() => Assert.Equal("2.5", cut.Find("#pref-long-timer").GetAttribute("value")));
            Assert.Equal("0", cut.Find("#pref-idle").GetAttribute("value"));
            Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));
        }

        [Fact]
        public void Save_Sends_Timer_Warning_Values()
        {
            var cut = Render<Profile>();
            cut.WaitForElement("#pref-long-timer");

            cut.Find("#pref-long-timer").Change("6");
            cut.Find("#pref-idle").Change("20");
            cut.FindAll("button").Single(b => b.TextContent.Contains("Save preferences")).Click();

            cut.WaitForAssertion(() => Assert.Contains(requests, r => r.Request.RequestUri!.AbsolutePath == "/User/me/preferences"));
            var body = JsonSerializer.Deserialize<UpdateUserPreferencesDto>(
                requests.Last(r => r.Request.RequestUri!.AbsolutePath == "/User/me/preferences").Body!, WebJson)!;
            Assert.Equal(6m, body.LongTimerWarningHours);
            Assert.Equal(20, body.IdleWarningMinutes);
        }

        [Fact]
        public void Browser_Toggle_Granted_Turns_On()
        {
            interop.Permission = "default";
            interop.RequestResult = "granted";
            var cut = Render<Profile>();

            cut.WaitForElement("[role=switch]").Click();

            cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[role=switch]").GetAttribute("aria-checked")));
            Assert.True(interop.OptIn);
            Assert.Equal("", cut.Find(".aurora-prefs__note").TextContent);
        }

        [Fact]
        public void Browser_Toggle_Denied_Stays_Off_With_Hint()
        {
            interop.Permission = "denied";
            var cut = Render<Profile>();

            cut.WaitForElement("[role=switch]").Click();

            cut.WaitForAssertion(() => Assert.Equal(Profile.BrowserDeniedHint, cut.Find(".aurora-prefs__note").TextContent));
            Assert.Equal("false", cut.Find("[role=switch]").GetAttribute("aria-checked"));
            Assert.False(interop.OptIn);
        }

        [Fact]
        public void Browser_Toggle_Unsupported_Explains()
        {
            interop.Permission = "unsupported";
            var cut = Render<Profile>();

            cut.WaitForElement("[role=switch]").Click();

            cut.WaitForAssertion(() => Assert.Equal(Profile.BrowserUnsupportedHint, cut.Find(".aurora-prefs__note").TextContent));
        }
    }
}
