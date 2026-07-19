using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Timinute.Server.Auth;
using Timinute.Server.Data;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Services.Pat;
using Xunit;

namespace Timinute.Server.Tests.Auth
{
    public class PatAuthenticationHandlerTest
    {
        private static string NewDbName() => Guid.NewGuid().ToString();

        private static ApplicationDbContext NewDb(string? dbName = null) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName ?? NewDbName()).Options);

        // Factory over a named InMemory store — mirrors production's IDbContextFactory, which the
        // handler now uses for the isolated LastUsedAt stamp. Pointing it at the same store as the
        // shared context lets a test observe the stamp landing.
        private sealed class NamedInMemoryFactory : IDbContextFactory<ApplicationDbContext>
        {
            private readonly string dbName;
            public NamedInMemoryFactory(string dbName) => this.dbName = dbName;
            public ApplicationDbContext CreateDbContext() =>
                new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options);
        }

        private static async Task<AuthenticateResult> Authenticate(
            ApplicationDbContext db, string? bearer, string? stampDbName = null)
        {
            var svc = new PatTokenService();

            // Options.Create(...) yields IOptions<T>, but the handler ctor requires
            // IOptionsMonitor<T> — Moq stands in for the monitor.
            var optionsMonitor = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
            optionsMonitor.Setup(x => x.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

            var factory = new NamedInMemoryFactory(stampDbName ?? NewDbName());
            var handler = new PatAuthenticationHandler(
                optionsMonitor.Object, NullLoggerFactory.Instance, UrlEncoder.Default, db, factory, svc);

            var ctx = new DefaultHttpContext();
            if (bearer != null) ctx.Request.Headers.Authorization = $"Bearer {bearer}";
            await handler.InitializeAsync(
                new AuthenticationScheme(PatAuthenticationHandler.SchemeName, null, typeof(PatAuthenticationHandler)), ctx);
            return await handler.AuthenticateAsync();
        }

        private static async Task<string> SeedToken(ApplicationDbContext db, PatTokenService svc,
            PatScope scope = PatScope.Read, DateTimeOffset? expires = null, DateTimeOffset? revoked = null)
        {
            var (plaintext, hash, prefix) = svc.Generate();
            db.PersonalAccessTokens.Add(new PersonalAccessToken
            {
                UserId = "user-1",
                Name = "t",
                TokenHash = hash,
                Prefix = prefix,
                Scopes = scope,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = expires,
                RevokedAt = revoked
            });
            await db.SaveChangesAsync();
            return plaintext;
        }

        [Fact]
        public async Task Valid_Token_Authenticates_With_UserId_And_Scope()
        {
            var dbName = Guid.NewGuid().ToString();
            var svc = new PatTokenService();
            string token;
            using (var seedDb = NewDb(dbName))
            {
                token = await SeedToken(seedDb, svc, PatScope.ReadWrite);
            }

            // A fresh context for authentication, sharing only the underlying store — this
            // mirrors production, where the PAT is created in an earlier, already-completed
            // HTTP request and authentication runs in a new request-scoped DbContext.
            using var authDb = NewDb(dbName);
            var result = await Authenticate(authDb, token);

            Assert.True(result.Succeeded);
            Assert.Equal("user-1", result.Principal!.FindFirstValue(Constants.Claims.UserId));
            Assert.Equal("read_write", result.Principal!.FindFirstValue(Constants.Claims.Scope));
            Assert.False(string.IsNullOrEmpty(result.Principal!.FindFirstValue(Constants.Claims.PatId)));
        }

        [Fact]
        public async Task Valid_Token_Leaves_No_Tracked_State_And_Stamps_LastUsedAt()
        {
            var dbName = Guid.NewGuid().ToString();
            var svc = new PatTokenService();
            string token;
            using (var seedDb = NewDb(dbName))
            {
                token = await SeedToken(seedDb, svc, PatScope.Read);
            }

            using var authDb = NewDb(dbName);
            // Point the stamp factory at the same store so the stamp is observable via authDb.
            var result = await Authenticate(authDb, token, stampDbName: dbName);
            Assert.True(result.Succeeded);

            // The shared, request-scoped context must carry no PAT tracked state once the
            // handler returns — the stamp is written through a separate factory context, so
            // nothing leaks onto the shared context.
            Assert.Empty(authDb.ChangeTracker.Entries<PersonalAccessToken>());

            // The stamp itself must have actually landed, and must not have clobbered any of
            // the token's other columns (guards against a naive attach-stub implementation
            // that overwrites the whole row with default values).
            var persisted = await authDb.PersonalAccessTokens.AsNoTracking().SingleAsync();
            Assert.NotNull(persisted.LastUsedAt);
            Assert.Equal("user-1", persisted.UserId);
            Assert.Equal("t", persisted.Name);
            Assert.Equal(PatScope.Read, persisted.Scopes);
        }

        [Fact]
        public async Task Failed_Authentication_Leaves_No_Tracked_State()
        {
            var db = NewDb();

            var result = await Authenticate(db, "tmn_pat_deadbeefdeadbeef");

            Assert.False(result.Succeeded);
            Assert.Empty(db.ChangeTracker.Entries<PersonalAccessToken>());
        }

        [Fact]
        public async Task Revoked_Token_Fails()
        {
            var db = NewDb();
            var svc = new PatTokenService();
            var token = await SeedToken(db, svc, revoked: DateTimeOffset.UtcNow);

            var result = await Authenticate(db, token);

            Assert.False(result.Succeeded);
        }

        [Fact]
        public async Task Expired_Token_Fails()
        {
            var db = NewDb();
            var svc = new PatTokenService();
            var token = await SeedToken(db, svc, expires: DateTimeOffset.UtcNow.AddMinutes(-1));

            var result = await Authenticate(db, token);

            Assert.False(result.Succeeded);
        }

        [Fact]
        public async Task Unknown_Token_Fails()
        {
            var result = await Authenticate(NewDb(), "tmn_pat_deadbeefdeadbeef");

            Assert.False(result.Succeeded);
        }

        [Fact]
        public async Task No_Header_NoResult()
        {
            var result = await Authenticate(NewDb(), null);

            Assert.False(result.Succeeded);
            Assert.True(result.None);
        }
    }
}
