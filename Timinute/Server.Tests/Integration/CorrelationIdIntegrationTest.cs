using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Timinute.Server.Tests.Integration
{
    [Collection("Integration")]
    public class CorrelationIdIntegrationTest : IClassFixture<TiminuteApiFactory>
    {
        private readonly HttpClient client;

        public CorrelationIdIntegrationTest(TiminuteApiFactory factory)
        {
            client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        }

        [Fact]
        public async Task Response_Carries_Generated_CorrelationId_When_None_Sent()
        {
            var response = await client.GetAsync("/");

            Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values),
                "X-Correlation-Id was not set");
            Assert.False(string.IsNullOrWhiteSpace(System.Linq.Enumerable.Single(values!)));
        }

        [Fact]
        public async Task Response_Echoes_Valid_Inbound_CorrelationId()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/");
            request.Headers.Add("X-Correlation-Id", "abc-123_DEF");

            var response = await client.SendAsync(request);

            var value = System.Linq.Enumerable.Single(response.Headers.GetValues("X-Correlation-Id"));
            Assert.Equal("abc-123_DEF", value);
        }

        [Fact]
        public async Task Response_Ignores_Invalid_Inbound_CorrelationId_And_Generates_Fresh_One()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/");
            var invalidValue = new string('a', 65); // over the 64-char limit
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", invalidValue);

            var response = await client.SendAsync(request);

            var value = System.Linq.Enumerable.Single(response.Headers.GetValues("X-Correlation-Id"));
            Assert.NotEqual(invalidValue, value);
            Assert.Matches(new Regex("^[0-9a-f]{32}$"), value);
        }

        [Fact]
        public async Task Response_Ignores_Inbound_CorrelationId_With_Invalid_Characters_And_Generates_Fresh_One()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/");
            var invalidValue = "bad value!"; // contains characters outside [A-Za-z0-9._-]
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", invalidValue);

            var response = await client.SendAsync(request);

            var value = System.Linq.Enumerable.Single(response.Headers.GetValues("X-Correlation-Id"));
            Assert.NotEqual(invalidValue, value);
            Assert.Matches(new Regex("^[0-9a-f]{32}$"), value);
        }
    }
}
