using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Shared.Dtos.Mcp;
using Xunit;

namespace Timinute.Server.Tests.Integration
{
    // Exercises the real pipeline (routing -> auth -> model binding -> controller) for the
    // read side of the MCP activity audit trail. TestAuthHandler authenticates every request
    // as the seeded ApplicationUser1 (see TestAuthHandler.HandleAuthenticateAsync), whose "sub"
    // claim maps to Constants.Claims.UserId - matching the convention already used by
    // PatControllerIntegrationTest.
    [Collection("Integration")]
    public class McpActivityControllerIntegrationTest : IClassFixture<TiminuteApiFactory>
    {
        private const string CallerUserId = "ApplicationUser1";
        private const string OtherUserId = "b2c3d4e5-f6a7-4b5c-8d7e-0f1a2b3c4d5e"; // seeded test2@email.com

        private readonly TiminuteApiFactory factory;
        private readonly HttpClient client;

        public McpActivityControllerIntegrationTest(TiminuteApiFactory factory)
        {
            this.factory = factory;
            client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        }

        [Fact]
        public async Task Activity_Endpoint_Returns_Array()
        {
            var rows = await client.GetFromJsonAsync<McpActivityDto[]>("/Pat/activity");
            Assert.NotNull(rows);   // empty is fine; proves the route + auth work
        }

        [Fact]
        public async Task Activity_Only_Returns_Callers_Own_Rows()
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                db.McpActivityLogs.Add(new McpActivityLog
                {
                    UserId = CallerUserId,
                    Timestamp = DateTimeOffset.UtcNow,
                    Tool = "list_projects",
                    Summary = "Listed 3 projects",
                    Result = McpActivityResult.Success
                });

                // Seeded directly through the DbContext for a different user - there is no
                // API path that would let the authenticated caller create a row for someone
                // else, so bypassing the API is the only way to prove the ownership filter.
                db.McpActivityLogs.Add(new McpActivityLog
                {
                    UserId = OtherUserId,
                    Timestamp = DateTimeOffset.UtcNow,
                    Tool = "list_projects",
                    Summary = "Someone else's activity",
                    Result = McpActivityResult.Success
                });

                await db.SaveChangesAsync();
            }

            var rows = await client.GetFromJsonAsync<McpActivityDto[]>("/Pat/activity");

            Assert.NotNull(rows);
            Assert.All(rows!, r => Assert.NotEqual("Someone else's activity", r.Summary));
            Assert.Contains(rows!, r => r.Summary == "Listed 3 projects");
        }

        [Fact]
        public async Task Activity_Returns_Newest_First()
        {
            var older = DateTimeOffset.UtcNow.AddMinutes(-30);
            var newer = DateTimeOffset.UtcNow;

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                db.McpActivityLogs.Add(new McpActivityLog
                {
                    UserId = CallerUserId,
                    Timestamp = older,
                    Tool = "create_time_entry",
                    Summary = "Older entry",
                    Result = McpActivityResult.Success
                });
                db.McpActivityLogs.Add(new McpActivityLog
                {
                    UserId = CallerUserId,
                    Timestamp = newer,
                    Tool = "create_time_entry",
                    Summary = "Newer entry",
                    Result = McpActivityResult.Success
                });

                await db.SaveChangesAsync();
            }

            var rows = await client.GetFromJsonAsync<McpActivityDto[]>("/Pat/activity");

            Assert.NotNull(rows);
            var newerIndex = Array.FindIndex(rows!, r => r.Summary == "Newer entry");
            var olderIndex = Array.FindIndex(rows!, r => r.Summary == "Older entry");
            Assert.True(newerIndex >= 0 && olderIndex >= 0);
            Assert.True(newerIndex < olderIndex, "Newest row must be ordered before the older row.");
        }
    }
}
