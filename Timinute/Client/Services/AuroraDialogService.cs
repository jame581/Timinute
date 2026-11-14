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
    public class AuroraDialogService
    {
        private readonly List<DialogEntry> stack = new();

        public IReadOnlyList<DialogEntry> Stack => stack;

        public event Action? Changed;

        /// <returns>The value passed to <see cref="Close"/>, or null when dismissed (Escape, ×, backdrop).</returns>
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

        /// <summary>Closes a specific dialog (used by the host for Escape/×/backdrop).</summary>
        public void Close(DialogEntry entry, object? result = null)
        {
            if (!stack.Remove(entry)) return;
            entry.Completion.TrySetResult(result);
            Changed?.Invoke();
        }

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
