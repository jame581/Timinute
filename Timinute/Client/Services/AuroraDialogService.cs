using Microsoft.AspNetCore.Components;
using Timinute.Client.Shared;

namespace Timinute.Client.Services
{
    /// <summary>One open dialog: what to render and the task its opener awaits.</summary>
    public sealed class DialogEntry
    {
        internal DialogEntry(string title, Type componentType, IDictionary<string, object?> parameters, string? width)
        {
            Title = title;
            ComponentType = componentType;
            Parameters = parameters;
            Width = width;
        }

        public Guid Id { get; } = Guid.NewGuid();
        public string Title { get; }
        public Type ComponentType { get; }
        public IDictionary<string, object?> Parameters { get; }

        /// <summary>CSS width of the panel on desktop (e.g. "640px"); null uses the default.</summary>
        public string? Width { get; }

        internal TaskCompletionSource<object?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Aurora-native replacement for Radzen's DialogService. Callers open a component as a modal
    /// and await its result; the component closes itself with <see cref="Close"/>. A single
    /// <see cref="AuroraDialogHost"/> in MainLayout renders the stack. Registered scoped, which in
    /// WASM is app lifetime, so the host and every caller share one instance.
    /// </summary>
    public class AuroraDialogService : IDisposable
    {
        private readonly List<DialogEntry> stack = new();
        private readonly NavigationManager? navigation;

        /// <summary>Test seam: no navigation, so dialogs only close explicitly.</summary>
        public AuroraDialogService()
        {
        }

        public AuroraDialogService(NavigationManager navigation)
        {
            // Like Radzen's DialogService: leaving the page (back gesture, auth redirect) closes
            // every dialog, so a sheet never outlives the page that opened it.
            this.navigation = navigation;
            navigation.LocationChanged += OnLocationChanged;
        }

        public IReadOnlyList<DialogEntry> Stack => stack;

        public event Action? Changed;

        /// <returns>The value passed to <see cref="Close"/>, or null when dismissed (Escape, ×, navigation).</returns>
        public Task<object?> OpenAsync<TComponent>(string title, Dictionary<string, object?>? parameters = null, string? width = null)
            where TComponent : IComponent
        {
            var entry = new DialogEntry(title, typeof(TComponent), parameters ?? new Dictionary<string, object?>(), width);
            stack.Add(entry);
            Changed?.Invoke();
            return entry.Completion.Task;
        }

        /// <summary>Closes the topmost dialog. A no-op when nothing is open.</summary>
        public void Close(object? result = null)
        {
            if (stack.Count > 0)
            {
                Close(stack[^1], result);
            }
        }

        /// <summary>
        /// Closes a specific dialog. Dialog bodies receive their entry as a cascading value and
        /// should close through it, so a late close (after an awaited save) can never hit a
        /// different dialog; closing an entry that is already gone is a no-op. A null entry
        /// closes the topmost dialog.
        /// </summary>
        public void Close(DialogEntry? entry, object? result)
        {
            if (entry == null)
            {
                Close(result);
                return;
            }

            if (!stack.Remove(entry)) return;
            entry.Completion.TrySetResult(result);
            Changed?.Invoke();
        }

        /// <summary>Closes every open dialog with a null result.</summary>
        public void CloseAll()
        {
            if (stack.Count == 0) return;
            var open = stack.ToArray();
            stack.Clear();
            foreach (var entry in open)
            {
                entry.Completion.TrySetResult(null);
            }

            Changed?.Invoke();
        }

        public void Dispose()
        {
            if (navigation != null)
            {
                navigation.LocationChanged -= OnLocationChanged;
            }
        }

        private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e) => CloseAll();

        public async Task<bool> ConfirmAsync(string message, string title, string okText = "OK", string cancelText = "Cancel", bool danger = false)
        {
            var result = await OpenAsync<AuroraConfirm>(title, new Dictionary<string, object?>
            {
                [nameof(AuroraConfirm.Message)] = message,
                [nameof(AuroraConfirm.OkText)] = okText,
                [nameof(AuroraConfirm.CancelText)] = cancelText,
                [nameof(AuroraConfirm.Danger)] = danger,
            }, width: "440px");
            return result is true;
        }
    }
}
