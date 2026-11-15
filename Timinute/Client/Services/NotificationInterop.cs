using Microsoft.JSInterop;

namespace Timinute.Client.Services
{
    /// <summary>
    /// Browser side of timer notifications: the per-device opt-in flag, the Notification API and
    /// the idle detector. Behind an interface so <see cref="TimerNotificationService"/> is testable.
    /// </summary>
    public interface INotificationInterop
    {
        Task<bool> GetBrowserOptInAsync();
        Task SetBrowserOptInAsync(bool on);

        /// <returns>"granted", "denied", "default" or "unsupported".</returns>
        Task<string> GetPermissionAsync();

        /// <returns>The permission after asking: "granted", "denied", "default" or "unsupported".</returns>
        Task<string> RequestPermissionAsync();

        Task ShowAsync(string title, string body, string tag);

        /// <summary>Calls <c>OnIdleEnded</c> / <c>OnOpenBellRequested</c> on <paramref name="target"/>.</summary>
        Task RegisterAsync(DotNetObjectReference<TimerNotificationService> target);

        Task StartIdleWatchAsync(int idleMinutes);
        Task StopIdleWatchAsync();
    }

    public sealed class NotificationInterop : INotificationInterop, IAsyncDisposable
    {
        internal const string OptInKey = "timinute.notifications.browser";

        private readonly IJSRuntime js;
        private Task<IJSObjectReference>? notifyModule;
        private Task<IJSObjectReference>? idleModule;

        public NotificationInterop(IJSRuntime js)
        {
            this.js = js;
        }

        public async Task<bool> GetBrowserOptInAsync()
        {
            try
            {
                return await js.InvokeAsync<string?>("localStorage.getItem", OptInKey) == "1";
            }
            catch (JSException)
            {
                return false;
            }
        }

        public async Task SetBrowserOptInAsync(bool on)
        {
            try
            {
                if (on)
                {
                    await js.InvokeVoidAsync("localStorage.setItem", OptInKey, "1");
                }
                else
                {
                    await js.InvokeVoidAsync("localStorage.removeItem", OptInKey);
                }
            }
            catch (JSException)
            {
                // Private mode / storage blocked: the choice lasts for this session only.
            }
        }

        public async Task<string> GetPermissionAsync() => await (await Notify()).InvokeAsync<string>("permission");

        public async Task<string> RequestPermissionAsync() => await (await Notify()).InvokeAsync<string>("request");

        public async Task ShowAsync(string title, string body, string tag) =>
            await (await Notify()).InvokeVoidAsync("show", title, body, tag);

        public async Task RegisterAsync(DotNetObjectReference<TimerNotificationService> target)
        {
            await (await Notify()).InvokeVoidAsync("registerOpenBell", target);
            await (await Idle()).InvokeVoidAsync("register", target);
        }

        public async Task StartIdleWatchAsync(int idleMinutes) => await (await Idle()).InvokeVoidAsync("startIdleWatch", idleMinutes);

        public async Task StopIdleWatchAsync() => await (await Idle()).InvokeVoidAsync("stopIdleWatch");

        public async ValueTask DisposeAsync()
        {
            foreach (var module in new[] { notifyModule, idleModule })
            {
                if (module is { IsCompletedSuccessfully: true })
                {
                    try { await module.Result.DisposeAsync(); }
                    catch (JSDisconnectedException) { }
                }
            }
        }

        private Task<IJSObjectReference> Notify() =>
            notifyModule ??= js.InvokeAsync<IJSObjectReference>("import", "./js/browser-notify.js").AsTask();

        private Task<IJSObjectReference> Idle() =>
            idleModule ??= js.InvokeAsync<IJSObjectReference>("import", "./js/idle.js").AsTask();
    }
}
