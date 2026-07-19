using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using Timinute.Server.Data;
using Timinute.Server.Helpers;
using Timinute.Server.Mcp;
using Timinute.Server.Models;
using Xunit;

namespace Timinute.Server.Tests.Mcp
{
    public class McpActivityInterceptorTest
    {
        // IDbContextFactory test double: hands out fresh InMemory contexts sharing one store
        // (keyed by name), mirroring the production factory that creates an independent context
        // per audit write.
        private sealed class InMemoryDbContextFactory : IDbContextFactory<ApplicationDbContext>
        {
            private readonly string dbName;
            public InMemoryDbContextFactory(string dbName) => this.dbName = dbName;

            public ApplicationDbContext CreateDbContext() =>
                new(new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(dbName).Options);
        }

        // Factory whose contexts throw on save — models a poisoned/broken audit write so we can
        // prove the interceptor swallows audit-write failures rather than masking the tool result.
        private sealed class ThrowingDbContextFactory : IDbContextFactory<ApplicationDbContext>
        {
            public ApplicationDbContext CreateDbContext() =>
                throw new InvalidOperationException("audit sink is down");
        }

        private static ApplicationDbContext ReadDb(string dbName) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options);

        private static McpActivitySink NewSink(string dbName) =>
            new(new InMemoryDbContextFactory(dbName));

        private static McpUserContext UserContext(string userId, bool canWrite, string? tokenId)
        {
            var claims = new System.Collections.Generic.List<Claim>
            {
                new(Constants.Claims.UserId, userId),
                new(Constants.Claims.Scope, canWrite ? "read_write" : "read"),
            };
            if (tokenId is not null) claims.Add(new Claim(Constants.Claims.PatId, tokenId));

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Pat"))
            };
            var accessor = new HttpContextAccessor { HttpContext = httpContext };
            return new McpUserContext(accessor);
        }

        private static McpActivityInterceptor NewInterceptor(McpActivitySink sink, McpUserContext user) =>
            new(sink, user, NullLogger<McpActivityInterceptor>.Instance);

        private static CallToolResult Ok() =>
            new() { Content = [new TextContentBlock { Text = "ok" }] };

        private static CallToolResult Error(string text) =>
            new() { IsError = true, Content = [new TextContentBlock { Text = text }] };

        // ---- Sink tests --------------------------------------------------------------------

        [Fact]
        public async Task Sink_Records_Success_Row()
        {
            var dbName = Guid.NewGuid().ToString();
            var sink = NewSink(dbName);

            await sink.WriteAsync("user-1", "tok-1", "list_projects", "Listed 3 projects",
                McpActivityResult.Success, null, CancellationToken.None);

            using var db = ReadDb(dbName);
            var row = db.McpActivityLogs.Single();
            Assert.Equal("list_projects", row.Tool);
            Assert.Equal(McpActivityResult.Success, row.Result);
            Assert.Equal("user-1", row.UserId);
            Assert.Equal("tok-1", row.TokenId);
            Assert.Null(row.Detail);
            Assert.NotEqual(default, row.Timestamp);
        }

        [Fact]
        public async Task Sink_Records_Failure_With_Detail()
        {
            var dbName = Guid.NewGuid().ToString();
            var sink = NewSink(dbName);

            await sink.WriteAsync("user-1", null, "log_time", "log_time failed",
                McpActivityResult.Failed, "This token is read-only.", CancellationToken.None);

            using var db = ReadDb(dbName);
            var row = db.McpActivityLogs.Single();
            Assert.Equal(McpActivityResult.Failed, row.Result);
            Assert.Equal("This token is read-only.", row.Detail);
            Assert.Null(row.TokenId);
        }

        [Fact]
        public async Task Sink_Truncates_Summary_And_Detail_To_512()
        {
            var dbName = Guid.NewGuid().ToString();
            var sink = NewSink(dbName);

            var longText = new string('x', 1000);
            await sink.WriteAsync("user-1", "tok-1", "log_time", longText,
                McpActivityResult.Failed, longText, CancellationToken.None);

            using var db = ReadDb(dbName);
            var row = db.McpActivityLogs.Single();
            Assert.Equal(512, row.Summary.Length);
            Assert.Equal(512, row.Detail!.Length);
        }

        // ---- Interceptor tests -------------------------------------------------------------

        [Fact]
        public async Task Interceptor_Successful_Tool_Records_One_Success_Row()
        {
            var dbName = Guid.NewGuid().ToString();
            var interceptor = NewInterceptor(NewSink(dbName), UserContext("user-1", canWrite: true, "tok-1"));

            var result = await interceptor.RunAsync("list_projects",
                () => new ValueTask<CallToolResult>(Ok()));

            Assert.False(result.IsError ?? false);

            using var db = ReadDb(dbName);
            var row = db.McpActivityLogs.Single();
            Assert.Equal("list_projects", row.Tool);
            Assert.Equal(McpActivityResult.Success, row.Result);
            Assert.Equal("user-1", row.UserId);
        }

        [Fact]
        public async Task Interceptor_Failing_Tool_Records_One_Failed_Row_And_Rethrows()
        {
            var dbName = Guid.NewGuid().ToString();
            var interceptor = NewInterceptor(NewSink(dbName), UserContext("user-1", canWrite: true, "tok-1"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                interceptor.RunAsync("log_time",
                    () => throw new InvalidOperationException("boom")).AsTask());

            Assert.Equal("boom", ex.Message);

            using var db = ReadDb(dbName);
            var row = db.McpActivityLogs.Single();
            Assert.Equal(McpActivityResult.Failed, row.Result);
            Assert.Equal("boom", row.Detail);
        }

        [Fact]
        public async Task Interceptor_Tool_Returning_IsError_Result_Records_Failed_Row()
        {
            // A tool that RETURNS an IsError result (rather than throwing) must still be audited
            // as Failed, with the error text captured as Detail.
            var dbName = Guid.NewGuid().ToString();
            var interceptor = NewInterceptor(NewSink(dbName), UserContext("user-1", canWrite: true, "tok-1"));

            var result = await interceptor.RunAsync("update_time_entry",
                () => new ValueTask<CallToolResult>(Error("Time entry not found.")));

            Assert.True(result.IsError);

            using var db = ReadDb(dbName);
            var row = db.McpActivityLogs.Single();
            Assert.Equal(McpActivityResult.Failed, row.Result);
            Assert.Equal("Time entry not found.", row.Detail);
        }

        [Fact]
        public async Task Interceptor_ReadOnly_Token_On_Write_Tool_ShortCircuits_Clean_Message_And_Records_Failed()
        {
            var dbName = Guid.NewGuid().ToString();
            var interceptor = NewInterceptor(NewSink(dbName), UserContext("user-1", canWrite: false, "tok-ro"));

            var toolCalled = false;
            var result = await interceptor.RunAsync("create_project", () =>
            {
                toolCalled = true;
                return new ValueTask<CallToolResult>(Ok());
            });

            // The tool body must never run for a denied write.
            Assert.False(toolCalled);

            // Clean client-visible message — exactly "This token is read-only.", no SDK prefix.
            Assert.True(result.IsError);
            var text = Assert.IsType<TextContentBlock>(result.Content.Single());
            Assert.Equal("This token is read-only.", text.Text);

            using var db = ReadDb(dbName);
            var row = db.McpActivityLogs.Single();
            Assert.Equal("create_project", row.Tool);
            Assert.Equal(McpActivityResult.Failed, row.Result);
            Assert.Equal("This token is read-only.", row.Detail);
        }

        [Fact]
        public async Task Interceptor_ReadOnly_Token_On_Read_Tool_Is_Allowed()
        {
            var dbName = Guid.NewGuid().ToString();
            var interceptor = NewInterceptor(NewSink(dbName), UserContext("user-1", canWrite: false, "tok-ro"));

            var result = await interceptor.RunAsync("list_projects",
                () => new ValueTask<CallToolResult>(Ok()));

            Assert.False(result.IsError ?? false);

            using var db = ReadDb(dbName);
            Assert.Equal(McpActivityResult.Success, db.McpActivityLogs.Single().Result);
        }

        [Fact]
        public async Task Interceptor_ReadWrite_Token_On_Write_Tool_Is_Allowed()
        {
            var dbName = Guid.NewGuid().ToString();
            var interceptor = NewInterceptor(NewSink(dbName), UserContext("user-1", canWrite: true, "tok-rw"));

            var toolCalled = false;
            var result = await interceptor.RunAsync("create_project", () =>
            {
                toolCalled = true;
                return new ValueTask<CallToolResult>(Ok());
            });

            Assert.True(toolCalled);
            Assert.False(result.IsError ?? false);
            using var db = ReadDb(dbName);
            Assert.Equal(McpActivityResult.Success, db.McpActivityLogs.Single().Result);
        }

        [Fact]
        public async Task Interceptor_Failed_Audit_Write_Does_Not_Fail_Successful_Tool()
        {
            // Audit sink is down (factory throws), but the tool succeeds — the interceptor must
            // swallow the audit failure and still return the tool's result.
            var interceptor = NewInterceptor(
                new McpActivitySink(new ThrowingDbContextFactory()),
                UserContext("user-1", canWrite: true, "tok-1"));

            var result = await interceptor.RunAsync("list_projects",
                () => new ValueTask<CallToolResult>(Ok()));

            Assert.False(result.IsError ?? false);
        }

        [Fact]
        public async Task Interceptor_Failed_Audit_Write_Still_Propagates_Tool_Exception()
        {
            // Both the tool and the audit write fail: the original tool exception must surface,
            // not the audit-write exception.
            var interceptor = NewInterceptor(
                new McpActivitySink(new ThrowingDbContextFactory()),
                UserContext("user-1", canWrite: true, "tok-1"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                interceptor.RunAsync("log_time",
                    () => throw new InvalidOperationException("tool boom")).AsTask());

            Assert.Equal("tool boom", ex.Message);
        }

        [Fact]
        public async Task Interceptor_Unknown_Tool_Records_A_Failed_Row()
        {
            // Deliberate decision: an unknown tool name still writes an audit row (the SDK's
            // inner handler throws McpProtocolException("Unknown tool: ...") which our try/catch
            // records as Failed before rethrowing). Captures tool-probing attempts.
            var dbName = Guid.NewGuid().ToString();
            var interceptor = NewInterceptor(NewSink(dbName), UserContext("user-1", canWrite: true, "tok-1"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                interceptor.RunAsync("does_not_exist",
                    () => throw new InvalidOperationException("Unknown tool: 'does_not_exist'")).AsTask());

            using var db = ReadDb(dbName);
            var row = db.McpActivityLogs.Single();
            Assert.Equal("does_not_exist", row.Tool);
            Assert.Equal(McpActivityResult.Failed, row.Result);
        }
    }
}
