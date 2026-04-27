using Microsoft.JSInterop;

namespace Timinute.Client.Services
{
    /// <summary>
    /// Single source of truth for whether the viewport is in the mobile breakpoint
    /// (≤768px). Backed by a tiny ES module that listens to a matchMedia change
    /// event and pushes updates here via JS interop.
    /// </summary>
    public class ViewportService : IAsyncDisposable
    {
        private readonly IJSRuntime js;
        private IJSObjectReference? module;
        private DotNetObjectReference<ViewportService>? selfRef;
        private bool initialized;

        public bool IsMobile { get; private set; }
        public event Action? OnChanged;

        public ViewportService(IJSRuntime js)
        {
            this.js = js;
        }

        /// <summary>
        /// Idempotent. Safe to call from any component that wants to ensure the
        /// service is bootstrapped — typically MainLayout in OnAfterRenderAsync(firstRender).
        /// </summary>
        public async ValueTask InitializeAsync()
        {
            if (initialized) return;
            initialized = true;

            try
            {
                module = await js.InvokeAsync<IJSObjectReference>("import", "./js/viewport.js");
                selfRef = DotNetObjectReference.Create(this);
                await module.InvokeVoidAsync("register", selfRef);
            }
            catch
            {
                // Prerendering or torn-down circuit — IsMobile stays false; OK as a default.
                initialized = false;
            }
        }

        [JSInvokable]
        public void SetIsMobile(bool value)
        {
            if (IsMobile == value) return;
            IsMobile = value;
            OnChanged?.Invoke();
        }

        public async ValueTask DisposeAsync()
        {
            if (module is not null)
            {
                try
                {
                    await module.InvokeVoidAsync("unregister");
                    await module.DisposeAsync();
                }
                catch (JSDisconnectedException) { }
            }
            selfRef?.Dispose();
        }
    }
}
