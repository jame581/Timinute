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
        private static ApplicationDbContext NewDb() =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static async Task<AuthenticateResult> Authenticate(ApplicationDbContext db, string? bearer)
        {
            var svc = new PatTokenService();

            // Options.Create(...) yields IOptions<T>, but the handler ctor requires
            // IOptionsMonitor<T> — Moq stands in for the monitor.
            var optionsMonitor = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
            optionsMonitor.Setup(x => x.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

            var handler = new PatAuthenticationHandler(
                optionsMonitor.Object, NullLoggerFactory.Instance, UrlEncoder.Default, db, svc);

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
            var db = NewDb();
            var svc = new PatTokenService();
            var token = await SeedToken(db, svc, PatScope.ReadWrite);

            var result = await Authenticate(db, token);

            Assert.True(result.Succeeded);
            Assert.Equal("user-1", result.Principal!.FindFirstValue(Constants.Claims.UserId));
            Assert.Equal("read_write", result.Principal!.FindFirstValue(Constants.Claims.Scope));
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
