using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Timinute.Server.Tests.Integration
{
    [Collection("Integration")]
    public class SecurityHeadersIntegrationTest : IClassFixture<TiminuteApiFactory>
    {
        private readonly HttpClient client;

        public SecurityHeadersIntegrationTest(TiminuteApiFactory factory)
        {
            client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        }

        private Task<HttpResponseMessage> GetAnyEndpointAsync()
        {
            // Any 200 through the real pipeline will do; this one is already
            // covered by ValidationIntegrationTest, so it is known-good.
            var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("o"));
            var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("o"));
            return client.GetAsync($"/Analytics/summary?From={from}&To={to}&TzOffsetMinutes=0");
        }

        [Theory]
        [InlineData("X-Content-Type-Options", "nosniff")]
        [InlineData("X-Frame-Options", "SAMEORIGIN")]
        [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
        public async Task Response_Carries_Baseline_Security_Header(string header, string expected)
        {
            var response = await GetAnyEndpointAsync();

            Assert.True(response.Headers.TryGetValues(header, out var values), $"{header} was not set");
            Assert.Equal(expected, values!.Single());
        }

        // Pins the middleware-order claim in Program.cs ("runs before the static-file
        // and Blazor-framework middleware so it covers those responses too"). "/" isn't
        // routed by MVC/Razor Pages, so a 200 here proves the request was served by
        // MapFallbackToFile("index.html") via the static-file pipeline (confirmed by the
        // ETag/Accept-Ranges response headers that only that middleware sets) and that
        // the security-header middleware still ran ahead of it.
        [Theory]
        [InlineData("X-Content-Type-Options", "nosniff")]
        [InlineData("X-Frame-Options", "SAMEORIGIN")]
        [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
        public async Task FallbackFile_Response_Carries_Baseline_Security_Header(string header, string expected)
        {
            var response = await client.GetAsync("/");

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("ETag", out _), "expected the static-file middleware to have handled this request (no ETag header present)");
            Assert.True(response.Headers.TryGetValues(header, out var values), $"{header} was not set");
            Assert.Equal(expected, values!.Single());
        }

        // Regression guard, not a style preference. Blazor WASM's OIDC stack renews the
        // access token silently by loading the authorize endpoint in a hidden same-origin
        // iframe. X-Frame-Options: DENY blocks same-origin framing as well as cross-origin,
        // which would break renewal and log users out when their token expired.
        [Fact]
        public async Task XFrameOptions_Must_Not_Be_DENY()
        {
            var response = await GetAnyEndpointAsync();

            var value = response.Headers.GetValues("X-Frame-Options").Single();

            Assert.NotEqual("DENY", value);
        }
    }
}
