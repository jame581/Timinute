using System.Globalization;
using Microsoft.JSInterop;
using Timinute.Client.Models;
using Timinute.Shared.Dtos;

namespace Timinute.Client.Services
{
    /// <summary>
    /// Evaluates the running-timer warnings (long-running, idle) for the notification bell and,
    /// when the user opted in on this device, mirrors new ones as browser notifications.
    /// Re-evaluates on every <see cref="TimerService.Changed"/> and every 30 s.
    /// </summary>
    public class TimerNotificationService : IAsyncDisposable
    {
        internal static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

        private readonly TimerService timer;
        private readonly UserProfileService profile;
        private readonly INotificationInterop interop;
        private readonly TimeProvider clock;

        private readonly List<BellItem> items = new();
        private readonly HashSet<string> notified = new();
        private readonly HashSet<string> keptIdle = new();

        private DateTimeOffset? trackedStart;
        private int dismissedMultiple;
        private int idleWatchMinutes;
        private bool initialized;
        private ITimer? loop;
        private DotNetObjectReference<TimerNotificationService>? selfRef;

        public TimerNotificationService(TimerService timer, UserProfileService profile, INotificationInterop interop, TimeProvider clock)
        {
            this.timer = timer;
            this.profile = profile;
            this.interop = interop;
            this.clock = clock;
        }

        public IReadOnlyList<BellItem> Items => items;

        public bool BrowserEnabled { get; private set; }

        public event Action? Changed;

        /// <summary>Raised when a browser notification is clicked: the bell should open.</summary>
        public event Action? OpenRequested;

        public async Task InitializeAsync()
        {
            if (initialized) return;
            initialized = true;

            timer.Changed += OnTimerChanged;
            BrowserEnabled = await interop.GetBrowserOptInAsync();
            try
            {
                selfRef = DotNetObjectReference.Create(this);
                await interop.RegisterAsync(selfRef);
            }
            catch (JSException)
            {
                // No idle detection / click-through without JS; the bell still works.
            }

            loop = clock.CreateTimer(_ => _ = SafeEvaluateAsync(), null, CheckInterval, CheckInterval);
            await SafeEvaluateAsync();
        }

        public async Task EvaluateAsync()
        {
            var current = timer.Current;
            var prefs = await GetPreferencesAsync();

            if (current == null)
            {
                trackedStart = null;
                dismissedMultiple = 0;
                keptIdle.Clear();
                items.Clear();
                await SetIdleWatchAsync(0);
                Changed?.Invoke();
                return;
            }

            if (trackedStart != current.StartDate)
            {
                // A different timer: forget dismissals and anything raised for the previous one.
                trackedStart = current.StartDate;
                dismissedMultiple = 0;
                keptIdle.Clear();
                items.RemoveAll(i => i.TimerStart != current.StartDate);
            }

            items.RemoveAll(i => i.Rule == BellRule.LongRunning);
            if (prefs.LongTimerWarningHours > 0)
            {
                var threshold = TimeSpan.FromHours((double)prefs.LongTimerWarningHours);
                var elapsed = timer.Elapsed;
                var multiple = (int)(elapsed.Ticks / threshold.Ticks);
                if (multiple >= 1 && multiple > dismissedMultiple)
                {
                    var item = new BellItem(
                        $"long:{current.StartDate.UtcTicks}:{multiple}",
                        BellRule.LongRunning,
                        $"Timer running for {FormatDuration(elapsed)}",
                        current.Name,
                        current.StartDate);
                    items.Insert(0, item);
                    await NotifyBrowserAsync(item);
                }
            }

            await SetIdleWatchAsync(prefs.IdleWarningMinutes);
            Changed?.Invoke();
        }

        public void Dismiss(string id)
        {
            var item = items.FirstOrDefault(i => i.Id == id);
            if (item == null) return;

            if (item.Rule == BellRule.LongRunning)
            {
                // Silence until the next multiple of the threshold (8 h dismissed -> back at 16 h).
                var multiple = int.Parse(id[(id.LastIndexOf(':') + 1)..], CultureInfo.InvariantCulture);
                dismissedMultiple = Math.Max(dismissedMultiple, multiple);
            }
            else
            {
                keptIdle.Add(id);
            }

            items.Remove(item);
            Changed?.Invoke();
        }

        public void KeepIdle(string id) => Dismiss(id);

        public async Task StopNowAsync()
        {
            await timer.StopAsync();
            await EvaluateAsync();
        }

        public async Task StopAtIdleStartAsync(string id)
        {
            var item = items.FirstOrDefault(i => i.Id == id && i.Rule == BellRule.Idle);
            if (item?.IdleStart == null) return;

            await timer.StopAsync(item.IdleStart);
            await EvaluateAsync();
        }

        /// <summary>Called by idle.js when activity resumes after a quiet period (device-clock UTC ISO strings).</summary>
        [JSInvokable]
        public async Task OnIdleEnded(string idleStartUtc, string idleEndUtc)
        {
            var current = timer.Current;
            if (current == null) return;

            var prefs = await GetPreferencesAsync();
            if (prefs.IdleWarningMinutes <= 0) return;

            // Device clock -> server clock, so "Stop at" is comparable with the timer's start.
            var offset = timer.ServerNow - clock.GetUtcNow();
            var start = DateTimeOffset.Parse(idleStartUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime() + offset;
            var end = DateTimeOffset.Parse(idleEndUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime() + offset;
            if (start < current.StartDate) start = current.StartDate;

            if (end - start < TimeSpan.FromMinutes(prefs.IdleWarningMinutes)) return;

            var id = $"idle:{current.StartDate.UtcTicks}:{start.UtcTicks}";
            if (keptIdle.Contains(id) || items.Any(i => i.Id == id)) return;

            var item = new BellItem(
                id,
                BellRule.Idle,
                $"You were idle {FormatTime(start)} – {FormatTime(end)}",
                current.Name,
                current.StartDate,
                start,
                end);
            items.Add(item);
            await NotifyBrowserAsync(item);
            Changed?.Invoke();
        }

        [JSInvokable]
        public Task OnOpenBellRequested()
        {
            OpenRequested?.Invoke();
            return Task.CompletedTask;
        }

        /// <returns>False when the browser doesn't support notifications or permission was denied.</returns>
        public async Task<bool> SetBrowserEnabledAsync(bool on)
        {
            if (!on)
            {
                BrowserEnabled = false;
                await interop.SetBrowserOptInAsync(false);
                Changed?.Invoke();
                return true;
            }

            var permission = await interop.GetPermissionAsync();
            if (permission == "default")
            {
                permission = await interop.RequestPermissionAsync();
            }

            if (permission != "granted")
            {
                BrowserEnabled = false;
                await interop.SetBrowserOptInAsync(false);
                Changed?.Invoke();
                return false;
            }

            BrowserEnabled = true;
            await interop.SetBrowserOptInAsync(true);
            Changed?.Invoke();
            return true;
        }

        /// <summary>The browser's permission for this site: granted, denied, default or unsupported.</summary>
        public Task<string> GetBrowserPermissionAsync() => interop.GetPermissionAsync();

        public async ValueTask DisposeAsync()
        {
            timer.Changed -= OnTimerChanged;
            if (loop != null)
            {
                await loop.DisposeAsync();
            }

            selfRef?.Dispose();
        }

        private void OnTimerChanged() => _ = SafeEvaluateAsync();

        private async Task SafeEvaluateAsync()
        {
            try
            {
                await EvaluateAsync();
            }
            catch
            {
                // Offline or signed out: keep the current items; the next tick retries.
            }
        }

        private async Task<UserPreferencesDto> GetPreferencesAsync() =>
            (await profile.GetCurrentAsync())?.Preferences ?? new UserPreferencesDto();

        private async Task NotifyBrowserAsync(BellItem item)
        {
            if (!BrowserEnabled || !notified.Add(item.Id)) return;
            try
            {
                await interop.ShowAsync(item.Title, item.Detail, $"{item.Rule}:{item.TimerStart.UtcTicks}");
            }
            catch (JSException)
            {
            }
        }

        private async Task SetIdleWatchAsync(int minutes)
        {
            minutes = timer.Current == null ? 0 : Math.Max(0, minutes);
            if (minutes == idleWatchMinutes) return;
            idleWatchMinutes = minutes;
            try
            {
                if (minutes > 0)
                {
                    await interop.StartIdleWatchAsync(minutes);
                }
                else
                {
                    await interop.StopIdleWatchAsync();
                }
            }
            catch (JSException)
            {
            }
        }

        internal static string FormatDuration(TimeSpan elapsed) => $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";

        private static string FormatTime(DateTimeOffset utc) => utc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
