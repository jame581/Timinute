using System.Net;
using System.Net.Http.Json;
using Blazored.SessionStorage;
using Microsoft.JSInterop;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.Timer;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Services
{
    /// <summary>
    /// Single source of truth for the running timer on the client. The timer itself lives on the
    /// server (<c>/Timer</c>), so it survives reloads, closed tabs and device switches; this service
    /// mirrors it, polls every 60 s and refreshes when the tab becomes visible again.
    /// </summary>
    public class TimerService : IAsyncDisposable
    {
        internal const string LegacySessionKey = "trackedTask";
        internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

        // Ignore sub-2s differences: the Date header has 1 s resolution plus network latency.
        private static readonly TimeSpan OffsetNoise = TimeSpan.FromSeconds(2);

        private readonly IHttpClientFactory clientFactory;
        private readonly ISessionStorageService sessionStorage;
        private readonly IJSRuntime js;
        private readonly TimeProvider clock;

        private ITimer? pollTimer;
        private DotNetObjectReference<TimerService>? selfRef;
        private IJSObjectReference? visibilityModule;
        private Task? initTask;
        private TimeSpan serverOffset = TimeSpan.Zero;

        public TimerService(IHttpClientFactory clientFactory, ISessionStorageService sessionStorage, IJSRuntime js, TimeProvider clock)
        {
            this.clientFactory = clientFactory;
            this.sessionStorage = sessionStorage;
            this.js = js;
            this.clock = clock;
        }

        public RunningTimerDto? Current { get; private set; }

        public event Action? Changed;

        /// <summary>Elapsed time on the server's clock (device clock corrected by the Date-header offset).</summary>
        public TimeSpan Elapsed
        {
            get
            {
                if (Current == null) return TimeSpan.Zero;
                var elapsed = clock.GetUtcNow() + serverOffset - Current.StartDate;
                return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
            }
        }

        /// <summary>The server's "now", for idle-trim stop times.</summary>
        public DateTimeOffset ServerNow => clock.GetUtcNow() + serverOffset;

        /// <summary>
        /// Idempotent: migrates a v2.4 tab-local timer, then starts polling. Concurrent callers (the
        /// layout and the tracker page both call it at boot) share one in-flight run; a failed run
        /// (offline at boot) is forgotten so the next call retries.
        /// </summary>
        public Task InitializeAsync()
        {
            if (initTask == null || initTask.IsFaulted || initTask.IsCanceled)
            {
                initTask = InitializeCoreAsync();
            }

            return initTask;
        }

        private async Task InitializeCoreAsync()
        {
            await MigrateLegacySessionTimerAsync();
            await RefreshAsync();

            pollTimer ??= clock.CreateTimer(_ => _ = SafeRefreshAsync(), null, PollInterval, PollInterval);
            await RegisterVisibilityAsync();
        }

        public async Task RefreshAsync()
        {
            var client = clientFactory.CreateClient(Constants.API.ClientName);
            using var response = await client.GetAsync(Constants.API.Timer.Get);
            CaptureServerOffset(response);

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                Current = null;
            }
            else
            {
                response.EnsureSuccessStatusCode();
                Current = await response.Content.ReadFromJsonAsync<RunningTimerDto>();
            }

            Changed?.Invoke();
        }

        /// <returns>False when another tab/device already runs a timer (the state is refreshed to show it).</returns>
        public async Task<bool> StartAsync(string name, string? projectId)
        {
            var client = clientFactory.CreateClient(Constants.API.ClientName);
            using var response = await client.PostAsJsonAsync(Constants.API.Timer.Start, new StartTimerDto
            {
                Name = name,
                ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId,
            });

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                await RefreshAsync();
                return false;
            }

            response.EnsureSuccessStatusCode();
            CaptureServerOffset(response);
            Current = await response.Content.ReadFromJsonAsync<RunningTimerDto>();
            Changed?.Invoke();
            return true;
        }

        /// <returns>The stop result, or null when the timer had already been stopped elsewhere.</returns>
        public async Task<StopTimerResultDto?> StopAsync(DateTimeOffset? endAt = null)
        {
            var client = clientFactory.CreateClient(Constants.API.ClientName);
            using var response = await client.PostAsJsonAsync(Constants.API.Timer.Stop, new StopTimerDto { EndAt = endAt });

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await RefreshAsync();
                return null;
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<StopTimerResultDto>();
            Current = null;
            Changed?.Invoke();
            return result;
        }

        public async Task DiscardAsync()
        {
            var client = clientFactory.CreateClient(Constants.API.ClientName);
            using var response = await client.DeleteAsync(Constants.API.Timer.Discard);
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
            }

            Current = null;
            Changed?.Invoke();
        }

        [JSInvokable]
        public Task OnVisible() => SafeRefreshAsync();

        public async ValueTask DisposeAsync()
        {
            if (pollTimer != null)
            {
                await pollTimer.DisposeAsync();
            }

            if (visibilityModule != null)
            {
                try
                {
                    await visibilityModule.InvokeVoidAsync("unregisterVisibility");
                    await visibilityModule.DisposeAsync();
                }
                catch (JSDisconnectedException)
                {
                }
            }

            selfRef?.Dispose();
        }

        private async Task SafeRefreshAsync()
        {
            try
            {
                await RefreshAsync();
            }
            catch
            {
                // Offline or signed out: keep the last known state; the next poll retries.
            }
        }

        private void CaptureServerOffset(HttpResponseMessage response)
        {
            if (response.Headers.Date is not { } serverDate) return;
            var offset = serverDate - clock.GetUtcNow();
            serverOffset = offset.Duration() < OffsetNoise ? TimeSpan.Zero : offset;
        }

        // v2.4 kept the running timer only in this tab's sessionStorage, backed by a 1-second
        // placeholder task. Stop it exactly the way v2.4 would have, so nothing is lost on upgrade.
        private async Task MigrateLegacySessionTimerAsync()
        {
            TrackedTask? legacy;
            try
            {
                legacy = await sessionStorage.GetItemAsync<TrackedTask>(LegacySessionKey);
            }
            catch
            {
                return;
            }

            if (legacy == null) return;

            try
            {
                var start = new DateTimeOffset(legacy.StartDate);
                var end = clock.GetUtcNow().ToLocalTime();
                if (end <= start) end = start.AddSeconds(1);

                var client = clientFactory.CreateClient(Constants.API.ClientName);
                using var response = await client.PutAsJsonAsync(Constants.API.TrackedTask.Update, new UpdateTrackedTaskDto
                {
                    TaskId = legacy.TaskId,
                    Name = legacy.Name,
                    ProjectId = legacy.ProjectId,
                    StartDate = start,
                    EndDate = end,
                });
            }
            catch
            {
                // Best effort, like v2.4's own stop: the key is cleared regardless.
            }
            finally
            {
                await sessionStorage.RemoveItemAsync(LegacySessionKey);
            }
        }

        private async Task RegisterVisibilityAsync()
        {
            if (selfRef != null) return;
            try
            {
                visibilityModule = await js.InvokeAsync<IJSObjectReference>("import", "./js/visibility.js");
                if (visibilityModule == null) return;
                selfRef = DotNetObjectReference.Create(this);
                await visibilityModule.InvokeVoidAsync("registerVisibility", selfRef);
            }
            catch
            {
                // Without the hook the 60 s poll still converges.
            }
        }
    }
}
