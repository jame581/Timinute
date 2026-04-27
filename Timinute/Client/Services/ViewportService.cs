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
        /// Idempotent on success; retries on failure. Safe to call from any
        /// component that wants to ensure the service is bootstrapped — typically
        /// MainLayout in OnAfterRenderAsync.
        /// </summary>
        public async ValueTask InitializeAsync()
        {
            if (initialized) return;

            IJSObjectReference? localModule = null;
            DotNetObjectReference<ViewportService>? localRef = null;
            try
            {
                localModule = await js.InvokeAsync<IJSObjectReference>("import", "./js/viewport.js");
                localRef = DotNetObjectReference.Create(this);
                await localModule.InvokeVoidAsync("register", localRef);

                // Only commit the registration once everything succeeded — otherwise we'd
                // leak partially-created handles across retries.
                module = localModule;
                selfRef = localRef;
                initialized = true;
            }
            catch
            {
                // Prerendering or torn-down circuit. Tear down any partial state so the
                // next render's retry starts from a clean slate; IsMobile stays false.
                localRef?.Dispose();
                if (localModule is not null)
                {
                    try { await localModule.DisposeAsync(); }
                    catch (JSDisconnectedException) { }
                    catch (JSException) { }
                }
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
            try
            {
                if (module is not null)
                {
                    try
                    {
                        await module.InvokeVoidAsync("unregister");
                        await module.DisposeAsync();
                    }
                    catch (JSDisconnectedException) { }
                    catch (JSException) { }
                }
            }
            finally
            {
                selfRef?.Dispose();
                selfRef = null;
                module = null;
            }
        }
    }
}
