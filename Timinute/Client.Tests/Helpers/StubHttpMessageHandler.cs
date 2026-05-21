using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Timinute.Client.Tests.Helpers
{
    // Test double for the HTTP pipeline. Counts how many requests actually
    // reached the wire and delegates each to a supplied responder, so
    // UserProfileService tests can assert that the session cache collapses
    // many GetCurrentAsync calls into a single GET /User/me.
    internal sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> responder;
        private int callCount;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            this.responder = responder;
        }

        // Interlocked because the concurrency test fires overlapping requests.
        public int CallCount => Volatile.Read(ref callCount);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref callCount);
            // Yield so the request is genuinely asynchronous, the way a browser
            // fetch always is. A synchronous Task.FromResult double would let
            // UserProfileService.FetchAsync complete inline — masking the
            // ordering its failure-retry path depends on (the catch nulls the
            // cache field only after GetCurrentAsync has assigned it).
            await Task.Yield();
            return await responder(request);
        }
    }
}
