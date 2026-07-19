using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Services.Pat;
using Xunit;

namespace Timinute.Server.Tests.Integration
{
    // Drives real tools/call JSON-RPC through the live /mcp pipeline (stateless Streamable HTTP,
    // single POST, no initialize handshake) to prove Task 8 end-to-end: the central filter is
    // wired, every call writes exactly one McpActivityLog row through the IDbContextFactory sink,
    // and a read-only token calling a write tool gets the clean "This token is read-only." message.
    [Collection("Integration")]
    public class McpActivityLogIntegrationTest : IClassFixture<TiminuteApiFactory>
    {
        private const string SeedUserId1 = "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d";

        private readonly TiminuteApiFactory factory;
        private readonly HttpClient client;

        public McpActivityLogIntegrationTest(TiminuteApiFactory factory)
        {
            this.factory = factory;
            client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        }

        private async Task<string> MintPat(PatScope scope)
        {
            var tokenService = factory.Services.GetRequiredService<IPatTokenService>();
            var (plaintext, hash, prefix) = tokenService.Generate();

            using var scopeSp = factory.Services.CreateScope();
            var db = scopeSp.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.PersonalAccessTokens.Add(new PersonalAccessToken
            {
                UserId = SeedUserId1,
                Name = $"mcp-activity-{scope}",
                TokenHash = hash,
                Prefix = prefix,
                Scopes = scope,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            return plaintext;
        }

        private async Task<string> CallTool(string pat, string toolName, string argumentsJson = "{}")
        {
            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\""
                + toolName + "\",\"arguments\":" + argumentsJson + "}}";
            var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {pat}");
            // Streamable HTTP requires the client accept both media types.
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");

            var res = await client.SendAsync(request);
            return await res.Content.ReadAsStringAsync();
        }

        private McpActivityLog LatestRowFor(string tool)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return db.McpActivityLogs
                .Where(r => r.UserId == SeedUserId1 && r.Tool == tool)
                .OrderByDescending(r => r.Timestamp)
                .AsEnumerable()
                .First();
        }

        [Fact]
        public async Task Read_Tool_Call_Writes_A_Success_Row()
        {
            var pat = await MintPat(PatScope.Read);

            var responseText = await CallTool(pat, "list_projects");

            // The SSE body carries the JSON-RPC result; a read tool must succeed.
            Assert.DoesNotContain("This token is read-only.", responseText);

            var row = LatestRowFor("list_projects");
            Assert.Equal(McpActivityResult.Success, row.Result);
            Assert.Equal(SeedUserId1, row.UserId);
        }

        [Fact]
        public async Task ReadOnly_Token_On_Write_Tool_Gets_Clean_Message_And_Failed_Row()
        {
            var pat = await MintPat(PatScope.Read);

            var responseText = await CallTool(pat, "create_project", """{"name":"Nope"}""");

            // Clean, client-visible message surfaced through the real pipeline (no SDK
            // "An error occurred invoking ..." wrapping).
            Assert.Contains("This token is read-only.", responseText);
            Assert.DoesNotContain("An error occurred invoking", responseText);

            var row = LatestRowFor("create_project");
            Assert.Equal(McpActivityResult.Failed, row.Result);
            Assert.Equal("This token is read-only.", row.Detail);
        }

        [Fact]
        public async Task Write_Tool_That_Fails_In_Body_Records_A_Failed_Row()
        {
            // A read_write token calling delete_time_entry with a bogus id: the tool throws
            // McpException("Time entry not found."), which propagates through the central filter's
            // try/catch. Proves a tool-body failure is audited as Failed (not Success) end-to-end.
            var pat = await MintPat(PatScope.ReadWrite);

            var responseText = await CallTool(pat, "delete_time_entry", """{"id":"does-not-exist"}""");

            // The SDK surfaces McpException messages, so the domain message reaches the client.
            Assert.Contains("Time entry not found.", responseText);

            var row = LatestRowFor("delete_time_entry");
            Assert.Equal(McpActivityResult.Failed, row.Result);
            Assert.Contains("Time entry not found.", row.Detail);
        }

        [Fact]
        public async Task Write_Tool_With_Invalid_Dto_Is_Rejected_And_Records_Failed_Row()
        {
            // A read_write token calling create_project with a malformed color: the DTO's
            // RegularExpression annotation is enforced at the app-service choke point (the MCP
            // path has no [ApiController] 422 short-circuit), surfaced as a clean McpException.
            // Proves DTO validation reaches /mcp end-to-end and is audited as Failed.
            var pat = await MintPat(PatScope.ReadWrite);

            var responseText = await CallTool(pat, "create_project", """{"name":"Valid Name","color":"not-a-color"}""");

            // The domain validation message (rethrown as McpException) reaches the client, and the
            // audit Detail records the clean, unwrapped message.
            Assert.Contains("Color must be a hex color in the form #RRGGBB.", responseText);

            var row = LatestRowFor("create_project");
            Assert.Equal(McpActivityResult.Failed, row.Result);
            Assert.Equal("Color must be a hex color in the form #RRGGBB.", row.Detail);
        }
    }
}
