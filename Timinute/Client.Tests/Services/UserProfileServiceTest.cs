using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using Timinute.Client.Helpers;
using Timinute.Client.Services;
using Timinute.Client.Tests.Helpers;
using Timinute.Shared.Dtos;
using Xunit;

namespace Timinute.Client.Tests.Services
{
    // Covers the P1-followups item 4: UserProfileService collapses the
    // 3-4 GET /User/me reads a session used to make into a single cached
    // fetch, with explicit invalidation after a preferences PUT.
    public class UserProfileServiceTest
    {
        // GetFromJsonAsync deserializes with web defaults; mirror that when
        // writing the canned responses so the wire format matches production.
        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

        [Fact]
        public async Task GetCurrentAsync_FetchesProfileFromServer()
        {
            var service = CreateService(_ => Task.FromResult(OkJson(SampleProfile())), out _);

            var profile = await service.GetCurrentAsync();

            Assert.NotNull(profile);
            Assert.Equal("Jan", profile!.FirstName);
            Assert.Equal(4, profile.TaskCount);
            Assert.Equal(ThemePreference.Dark, profile.Preferences.Theme);
        }

        [Fact]
        public async Task GetCurrentAsync_RequestsTheUserMeEndpoint()
        {
            Uri? requested = null;
            var service = CreateService(req =>
            {
                requested = req.RequestUri;
                return Task.FromResult(OkJson(SampleProfile()));
            }, out _);

            await service.GetCurrentAsync();

            Assert.NotNull(requested);
            Assert.Equal("/User/me", requested!.AbsolutePath);
        }

        [Fact]
        public async Task GetCurrentAsync_SecondCall_ReturnsCachedResultWithoutRefetching()
        {
            var service = CreateService(_ => Task.FromResult(OkJson(SampleProfile())), out var handler);

            var first = await service.GetCurrentAsync();
            var second = await service.GetCurrentAsync();

            Assert.Equal(1, handler.CallCount);
            Assert.Same(first, second);
        }

        [Fact]
        public async Task GetCurrentAsync_ConcurrentCallers_ShareSingleInFlightRequest()
        {
            // Hold the response open so all five callers arrive before the
            // first request completes — the genuine in-flight-sharing case.
            var gate = new TaskCompletionSource();
            var service = CreateService(async _ =>
            {
                await gate.Task;
                return OkJson(SampleProfile());
            }, out var handler);

            var calls = Enumerable.Range(0, 5)
                .Select(_ => service.GetCurrentAsync())
                .ToArray();
            gate.SetResult();
            await Task.WhenAll(calls);

            Assert.Equal(1, handler.CallCount);
            Assert.All(calls, t => Assert.NotNull(t.Result));
        }

        [Fact]
        public async Task InvalidateAsync_ClearsCache_NextCallRefetches()
        {
            var service = CreateService(_ => Task.FromResult(OkJson(SampleProfile())), out var handler);

            await service.GetCurrentAsync();
            await service.InvalidateAsync();
            await service.GetCurrentAsync();

            Assert.Equal(2, handler.CallCount);
        }

        [Fact]
        public async Task GetCurrentAsync_OnServerError_ReturnsNull()
        {
            var service = CreateService(
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)),
                out _);

            var profile = await service.GetCurrentAsync();

            Assert.Null(profile);
        }

        [Fact]
        public async Task GetCurrentAsync_AfterFailure_RetriesOnNextCall()
        {
            // A failed fetch must not poison the cache: the first call 500s,
            // the second must hit the wire again and succeed.
            var responses = new Queue<HttpResponseMessage>(new[]
            {
                new HttpResponseMessage(HttpStatusCode.InternalServerError),
                OkJson(SampleProfile()),
            });
            var service = CreateService(_ => Task.FromResult(responses.Dequeue()), out var handler);

            var first = await service.GetCurrentAsync();
            var second = await service.GetCurrentAsync();

            Assert.Null(first);
            Assert.NotNull(second);
            Assert.Equal(2, handler.CallCount);
        }

        [Fact]
        public async Task GetCurrentAsync_RaisesChangedEventWithFetchedProfile()
        {
            var service = CreateService(_ => Task.FromResult(OkJson(SampleProfile())), out _);
            UserProfileDto? captured = null;
            var raised = 0;
            service.Changed += p => { captured = p; raised++; };

            var profile = await service.GetCurrentAsync();

            Assert.Equal(1, raised);
            Assert.Same(profile, captured);
        }

        private static UserProfileService CreateService(
            Func<HttpRequestMessage, Task<HttpResponseMessage>> responder,
            out StubHttpMessageHandler handler)
        {
            handler = new StubHttpMessageHandler(responder);
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(Constants.API.ClientName)).Returns(client);
            return new UserProfileService(factory.Object);
        }

        private static HttpResponseMessage OkJson(UserProfileDto profile)
            => new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(profile, options: WebJson),
            };

        private static UserProfileDto SampleProfile() => new()
        {
            FirstName = "Jan",
            LastName = "Testovic",
            Email = "test1@email.com",
            CreatedAt = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TotalTrackedTime = TimeSpan.FromHours(14),
            ProjectCount = 3,
            TaskCount = 4,
            Preferences = new UserPreferencesDto
            {
                Theme = ThemePreference.Dark,
                WeeklyGoalHours = 40.0m,
                WorkdayHoursPerDay = 8.0m,
            },
        };
    }
}
