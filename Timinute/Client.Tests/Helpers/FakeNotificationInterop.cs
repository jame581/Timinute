using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Timinute.Client.Services;

namespace Timinute.Client.Tests.Helpers
{
    public sealed class FakeNotificationInterop : INotificationInterop
    {
        public bool OptIn { get; set; }
        public string Permission { get; set; } = "default";
        public string RequestResult { get; set; } = "granted";
        public List<(string Title, string Body, string Tag)> Shown { get; } = new();
        public bool IdleWatching { get; private set; }
        public int IdleMinutes { get; private set; }

        public Task<bool> GetBrowserOptInAsync() => Task.FromResult(OptIn);
        public Task SetBrowserOptInAsync(bool on) { OptIn = on; return Task.CompletedTask; }
        public Task<string> GetPermissionAsync() => Task.FromResult(Permission);
        public Task<string> RequestPermissionAsync() { Permission = RequestResult; return Task.FromResult(RequestResult); }
        public Task ShowAsync(string title, string body, string tag) { Shown.Add((title, body, tag)); return Task.CompletedTask; }
        public Task RegisterAsync(DotNetObjectReference<TimerNotificationService> target) => Task.CompletedTask;
        public Task StartIdleWatchAsync(int idleMinutes) { IdleWatching = true; IdleMinutes = idleMinutes; return Task.CompletedTask; }
        public Task StopIdleWatchAsync() { IdleWatching = false; return Task.CompletedTask; }
    }
}
