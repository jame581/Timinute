using System;
using System.Collections.Generic;
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
using Timinute.Shared.Dtos.Timer;
using Timinute.Shared.Dtos.TrackedTask;
using Xunit;

namespace Timinute.Client.Tests.Services
{
    public class TimerServiceTest
    {
        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
        private static readonly DateTimeOffset Now = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

        private readonly FakeTimeProvider clock = new(Now);
        private readonly Mock<ISessionStorageService> session = new();
        private readonly List<HttpRequestMessage> requests = new();

        private TimerService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            var handler = new StubHttpMessageHandler(req =>
            {
                requests.Add(req);
                return Task.FromResult(responder(req));
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(Constants.API.ClientName)).Returns(client);
            return new TimerService(factory.Object, session.Object, new Mock<IJSRuntime>().Object, clock);
        }

        private static HttpResponseMessage Json<T>(T body, HttpStatusCode status = HttpStatusCode.OK, DateTimeOffset? date = null)
        {
            var response = new HttpResponseMessage(status) { Content = JsonContent.Create(body, options: WebJson) };
            if (date.HasValue) response.Headers.Date = date;
            return response;
        }

        private static RunningTimerDto Running(DateTimeOffset start) => new() { Name = "Deep work", StartDate = start };

        [Fact]
        public async Task Refresh_NoContent_SetsCurrentNull_RaisesChanged()
        {
            var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            var raised = 0;
            service.Changed += () => raised++;

            await service.RefreshAsync();

            Assert.Null(service.Current);
            Assert.Equal(1, raised);
            Assert.Equal("/Timer", requests[0].RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task Refresh_Ok_SetsCurrent()
        {
            var service = CreateService(_ => Json(Running(Now.AddMinutes(-30))));

            await service.RefreshAsync();

            Assert.Equal("Deep work", service.Current!.Name);
            Assert.Equal(TimeSpan.FromMinutes(30), service.Elapsed);
        }

        [Fact]
        public async Task Elapsed_UsesServerClockOffset()
        {
            // Server clock is 2 minutes ahead of this device.
            var service = CreateService(_ => Json(Running(Now.AddMinutes(-10)), date: Now.AddMinutes(2)));

            await service.RefreshAsync();

            Assert.Equal(TimeSpan.FromMinutes(12), service.Elapsed);
        }

        [Fact]
        public async Task Start_Created_SetsCurrent_ReturnsTrue()
        {
            var service = CreateService(req => Json(Running(Now), HttpStatusCode.Created));

            Assert.True(await service.StartAsync("Deep work", "p1"));

            Assert.NotNull(service.Current);
            Assert.Equal(HttpMethod.Post, requests[0].Method);
            Assert.Equal("/Timer/start", requests[0].RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task Start_Conflict_ReturnsFalse_AndRefreshes()
        {
            var service = CreateService(req => req.Method == HttpMethod.Post
                ? new HttpResponseMessage(HttpStatusCode.Conflict)
                : Json(Running(Now.AddMinutes(-5))));

            Assert.False(await service.StartAsync("Mine", null));

            Assert.Equal("Deep work", service.Current!.Name);
            Assert.Equal(HttpMethod.Get, requests[1].Method);
        }

        [Fact]
        public async Task Stop_PostsEndAt_ClearsCurrent()
        {
            var service = CreateService(req => req.Method == HttpMethod.Get
                ? Json(Running(Now.AddHours(-1)))
                : Json(new StopTimerResultDto { TrackedTask = new TrackedTaskDto { Name = "Deep work" } }));
            await service.RefreshAsync();
            var endAt = Now.AddMinutes(-20);

            var result = await service.StopAsync(endAt);

            Assert.NotNull(result);
            Assert.Null(service.Current);
            var stop = requests[^1];
            Assert.Equal("/Timer/stop", stop.RequestUri!.AbsolutePath);
            var body = await stop.Content!.ReadFromJsonAsync<StopTimerDto>(WebJson);
            Assert.Equal(endAt, body!.EndAt);
        }

        [Fact]
        public async Task Discard_SendsDelete_ClearsCurrent()
        {
            var service = CreateService(req => req.Method == HttpMethod.Get
                ? Json(Running(Now))
                : new HttpResponseMessage(HttpStatusCode.NoContent));
            await service.RefreshAsync();

            await service.DiscardAsync();

            Assert.Null(service.Current);
            Assert.Equal(HttpMethod.Delete, requests[^1].Method);
        }

        [Fact]
        public async Task Legacy_SessionTimer_IsStoppedOnce_AndKeyRemoved()
        {
            var legacyStart = Now.AddMinutes(-45).LocalDateTime;
            session.Setup(s => s.GetItemAsync<TrackedTask>("trackedTask", default))
                   .ReturnsAsync(new TrackedTask { TaskId = "legacy-1", Name = "Old tab", StartDate = legacyStart });
            var service = CreateService(req => req.Method == HttpMethod.Put
                ? Json(new TrackedTaskDto())
                : new HttpResponseMessage(HttpStatusCode.NoContent));

            await service.InitializeAsync();
            await service.InitializeAsync();

            var puts = requests.FindAll(r => r.Method == HttpMethod.Put);
            Assert.Single(puts);
            Assert.Equal("/TrackedTask", puts[0].RequestUri!.AbsolutePath);
            var body = await puts[0].Content!.ReadFromJsonAsync<UpdateTrackedTaskDto>(WebJson);
            Assert.Equal("legacy-1", body!.TaskId);
            Assert.Equal(Now, body.EndDate!.Value.ToUniversalTime());
            session.Verify(s => s.RemoveItemAsync("trackedTask", default), Times.Once);
        }

        [Fact]
        public async Task Initialize_Concurrent_Callers_Migrate_Once()
        {
            session.Setup(s => s.GetItemAsync<TrackedTask>("trackedTask", default))
                   .ReturnsAsync(new TrackedTask { TaskId = "legacy-2", Name = "Old tab", StartDate = Now.AddMinutes(-5).LocalDateTime });
            var service = CreateService(req => req.Method == HttpMethod.Put
                ? Json(new TrackedTaskDto())
                : new HttpResponseMessage(HttpStatusCode.NoContent));

            await Task.WhenAll(service.InitializeAsync(), service.InitializeAsync());

            Assert.Single(requests.FindAll(r => r.Method == HttpMethod.Put));
        }

        [Fact]
        public async Task Initialize_AfterFailure_Retries()
        {
            var fail = true;
            var service = CreateService(_ => fail
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : Json(Running(Now)));

            await Assert.ThrowsAsync<HttpRequestException>(() => service.InitializeAsync());
            fail = false;
            await service.InitializeAsync();

            Assert.NotNull(service.Current);
        }

        [Fact]
        public async Task Legacy_PutServerError_KeepsKey_ForRetry()
        {
            session.Setup(s => s.GetItemAsync<TrackedTask>("trackedTask", default))
                   .ReturnsAsync(new TrackedTask { TaskId = "legacy-3", Name = "Old tab", StartDate = Now.AddHours(-3).LocalDateTime });
            var service = CreateService(req => req.Method == HttpMethod.Put
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.NoContent));

            await service.InitializeAsync();

            session.Verify(s => s.RemoveItemAsync("trackedTask", default), Times.Never);
        }

        [Fact]
        public async Task Legacy_ProjectGone_RetriesWithoutProject_ThenRemovesKey()
        {
            session.Setup(s => s.GetItemAsync<TrackedTask>("trackedTask", default))
                   .ReturnsAsync(new TrackedTask { TaskId = "legacy-4", Name = "Old tab", ProjectId = "trashed", StartDate = Now.AddHours(-1).LocalDateTime });
            var bodies = new List<UpdateTrackedTaskDto>();
            var service = CreateService(req =>
            {
                if (req.Method != HttpMethod.Put) return new HttpResponseMessage(HttpStatusCode.NoContent);
                var body = req.Content!.ReadFromJsonAsync<UpdateTrackedTaskDto>(WebJson).GetAwaiter().GetResult()!;
                bodies.Add(body);
                return body.ProjectId == null ? Json(new TrackedTaskDto()) : new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            await service.InitializeAsync();

            Assert.Equal(2, bodies.Count);
            Assert.Null(bodies[1].ProjectId);
            session.Verify(s => s.RemoveItemAsync("trackedTask", default), Times.Once);
        }

        [Fact]
        public async Task IsReady_OnlyAfterSuccessfulInit()
        {
            var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            Assert.False(service.IsReady);

            await service.InitializeAsync();

            Assert.True(service.IsReady);
        }

        [Fact]
        public async Task Poll_Refreshes_Every_60_Seconds()
        {
            var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            await service.InitializeAsync();
            var afterInit = requests.Count;

            clock.Advance(TimeSpan.FromSeconds(60));
            await Task.Delay(50);

            Assert.True(requests.Count > afterInit);
        }
    }
}
