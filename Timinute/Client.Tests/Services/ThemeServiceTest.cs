using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using Timinute.Client.Services;
using Timinute.Shared.Dtos;
using Xunit;

namespace Timinute.Client.Tests.Services
{
    // Covers the P1-followups item 3: ThemeService gains an OS-color-scheme
    // change listener bridged from theme-bootstrap.js — RegisterOsChangeListenerAsync,
    // the [JSInvokable] NotifyResolvedThemeChangedAsync callback, and the
    // register/unregister disposal handshake.
    //
    // Note: ThemeService calls IJSRuntime.InvokeVoidAsync, an extension method
    // that delegates to InvokeAsync<IJSVoidResult>(identifier, args). Moq can
    // only see the underlying interface method, so the setups and verifies
    // below target InvokeAsync<IJSVoidResult>.
    public class ThemeServiceTest
    {
        [Fact]
        public async Task RegisterOsChangeListenerAsync_InvokesThemeRegister()
        {
            var js = new Mock<IJSRuntime>();
            var service = CreateService(js);

            await service.RegisterOsChangeListenerAsync();

            js.Verify(
                j => j.InvokeAsync<IJSVoidResult>("__theme.register", It.IsAny<object?[]?>()),
                Times.Once);
        }

        [Fact]
        public async Task RegisterOsChangeListenerAsync_CalledTwice_RegistersOnlyOnce()
        {
            // Idempotent: MainLayout calls this on every render, so only the
            // first call may reach JS.
            var js = new Mock<IJSRuntime>();
            var service = CreateService(js);

            await service.RegisterOsChangeListenerAsync();
            await service.RegisterOsChangeListenerAsync();

            js.Verify(
                j => j.InvokeAsync<IJSVoidResult>("__theme.register", It.IsAny<object?[]?>()),
                Times.Once);
        }

        [Fact]
        public async Task RegisterOsChangeListenerAsync_WhenBootstrapAbsent_SwallowsAndAllowsRetry()
        {
            // No theme-bootstrap.js (e.g. prerender race): the JSException is
            // swallowed and selfRef is reset, so a later render retries.
            var js = new Mock<IJSRuntime>();
            js.Setup(j => j.InvokeAsync<IJSVoidResult>(It.IsAny<string>(), It.IsAny<object?[]?>()))
                .Throws(new JSException("__theme is not defined"));
            var service = CreateService(js);

            var ex = await Record.ExceptionAsync(() => service.RegisterOsChangeListenerAsync());
            await service.RegisterOsChangeListenerAsync();

            Assert.Null(ex);
            js.Verify(
                j => j.InvokeAsync<IJSVoidResult>("__theme.register", It.IsAny<object?[]?>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task NotifyResolvedThemeChangedAsync_FiresChangedEventWithSystem()
        {
            var service = CreateService(new Mock<IJSRuntime>());
            ThemePreference? captured = null;
            service.Changed += p => captured = p;

            await service.NotifyResolvedThemeChangedAsync();

            Assert.Equal(ThemePreference.System, captured);
        }

        [Fact]
        public async Task NotifyResolvedThemeChangedAsync_WithNoSubscribers_DoesNotThrow()
        {
            var service = CreateService(new Mock<IJSRuntime>());

            var ex = await Record.ExceptionAsync(() => service.NotifyResolvedThemeChangedAsync());

            Assert.Null(ex);
        }

        [Fact]
        public async Task DisposeAsync_AfterRegister_InvokesThemeUnregister()
        {
            var js = new Mock<IJSRuntime>();
            var service = CreateService(js);
            await service.RegisterOsChangeListenerAsync();

            await service.DisposeAsync();

            js.Verify(
                j => j.InvokeAsync<IJSVoidResult>("__theme.unregister", It.IsAny<object?[]?>()),
                Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_WithoutRegister_DoesNotTouchJs()
        {
            var js = new Mock<IJSRuntime>();
            var service = CreateService(js);

            await service.DisposeAsync();

            js.Verify(
                j => j.InvokeAsync<IJSVoidResult>(It.IsAny<string>(), It.IsAny<object?[]?>()),
                Times.Never);
        }

        [Fact]
        public async Task Dispose_AfterRegister_DoesNotThrow()
        {
            // The sync IDisposable path can't call JS (disconnected circuit);
            // it must still release selfRef without throwing.
            var service = CreateService(new Mock<IJSRuntime>());
            await service.RegisterOsChangeListenerAsync();

            var ex = Record.Exception(() => service.Dispose());

            Assert.Null(ex);
        }

        private static ThemeService CreateService(Mock<IJSRuntime> js)
        {
            // ThemeService needs a UserProfileService collaborator, but the
            // OS-listener paths under test never touch it — a service over an
            // unused factory is enough.
            var factory = new Mock<IHttpClientFactory>();
            var profileService = new UserProfileService(factory.Object);
            return new ThemeService(js.Object, factory.Object, profileService);
        }
    }
}
