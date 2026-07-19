using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Services.Pat;
using Xunit;

namespace Timinute.Server.Tests.Integration
{
    // Asserts the REAL security envelope of the MCP endpoint (plan revision R7):
    // /mcp pins AuthenticationSchemes = "Pat", so the suite's TestAuthHandler is
    // bypassed there. A request with no valid PAT must be rejected (401); a request
    // carrying a valid PAT bearer must clear auth and reach MapMcp (any non-401 proves it).
    [Collection("Integration")]
    public class McpEndpointIntegrationTest : IClassFixture<TiminuteApiFactory>
    {
        // Seeded ApplicationUser1 (test1@email.com) - ApplicationDbContext.SeedUserId1.
        private const string SeedUserId1 = "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d";

        private readonly TiminuteApiFactory factory;
        private readonly HttpClient client;

        public McpEndpointIntegrationTest(TiminuteApiFactory factory)
        {
            this.factory = factory;
            client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        }

        [Fact]
        public async Task Mcp_Endpoint_Rejects_Anonymous_With_401()
        {
            // No Authorization header: the Pat scheme yields NoResult, RequireAuthorization
            // challenges the Pat scheme, and the endpoint responds 401. (While /mcp does not
            // exist yet this fails as 404 - the TDD red state.)
            var res = await client.PostAsync("/mcp",
                new StringContent("{}", Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);

            // RFC 9110: the 401 must carry a WWW-Authenticate challenge starting with "Bearer".
            Assert.True(res.Headers.WwwAuthenticate.Count > 0, "Missing WWW-Authenticate header.");
            Assert.StartsWith("Bearer", res.Headers.WwwAuthenticate.ToString());
        }

        [Fact]
        public async Task Mcp_Endpoint_Accepts_Valid_Pat_Bearer()
        {
            // Mint a real token via PatTokenService and seed the hash+prefix directly into the
            // factory's InMemory store, owned by the seeded fixture user.
            var tokenService = factory.Services.GetRequiredService<IPatTokenService>();
            var (plaintext, hash, prefix) = tokenService.Generate();

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.PersonalAccessTokens.Add(new PersonalAccessToken
                {
                    UserId = SeedUserId1,
                    Name = "mcp-integration",
                    TokenHash = hash,
                    Prefix = prefix,
                    Scopes = PatScope.ReadWrite,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {plaintext}");

            var res = await client.SendAsync(request);

            // Any non-401 (200/400/406) proves the PAT authenticated and MapMcp answered.
            Assert.NotEqual(HttpStatusCode.Unauthorized, res.StatusCode);
        }
    }
}
