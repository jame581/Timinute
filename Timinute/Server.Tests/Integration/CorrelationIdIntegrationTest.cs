using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Timinute.Server.Tests.Integration
{
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
    }
}
