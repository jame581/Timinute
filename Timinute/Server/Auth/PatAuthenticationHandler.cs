using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Timinute.Server.Data;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Services.Pat;

namespace Timinute.Server.Auth
{
    // Validates tmn_pat_… bearer tokens against PersonalAccessTokens. Registered as an
    // authentication scheme but deliberately left out of the ApplicationDefinedPolicy
    // ForwardDefaultSelector in Program.cs — PATs authenticate only where an endpoint
    // explicitly opts in (the /mcp endpoint, via RequireAuthorization), never on the
    // general Bearer→JwtBearer path used by REST controllers.
    public sealed class PatAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Pat";

        private readonly ApplicationDbContext db;
        private readonly IDbContextFactory<ApplicationDbContext> dbFactory;
        private readonly IPatTokenService tokens;

        public PatAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
            UrlEncoder encoder, ApplicationDbContext db,
            IDbContextFactory<ApplicationDbContext> dbFactory, IPatTokenService tokens)
            : base(options, logger, encoder)
        {
            this.db = db;
            this.dbFactory = dbFactory;
            this.tokens = tokens;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // TrimStart to mirror the ForwardDefaultSelector's own header check in Program.cs.
            var authorization = Request.Headers.Authorization.ToString().TrimStart();
            if (string.IsNullOrWhiteSpace(authorization) ||
                !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return AuthenticateResult.NoResult();

            var raw = authorization.Substring("Bearer ".Length).Trim();
            if (!raw.StartsWith(IPatTokenService.TokenPrefix, StringComparison.Ordinal))
                return AuthenticateResult.NoResult();

            var body = raw.Substring(IPatTokenService.TokenPrefix.Length);
            if (body.Length < 8)
                return AuthenticateResult.Fail("Malformed token.");

            var prefix = body.Substring(0, 8);
            var hash = tokens.Hash(raw);

            // AsNoTracking: this handler must never leave PAT entities tracked on the
            // shared, request-scoped ApplicationDbContext — repositories and controllers
            // reuse the same instance for the rest of the request.
            var candidates = await db.PersonalAccessTokens
                .AsNoTracking()
                .Where(t => t.Prefix == prefix).ToListAsync();

            var now = DateTimeOffset.UtcNow;
            var match = candidates.FirstOrDefault(t =>
                tokens.FixedTimeEquals(t.TokenHash, hash)
                && t.RevokedAt == null
                && (t.ExpiresAt == null || t.ExpiresAt > now));

            if (match is null)
                return AuthenticateResult.Fail("Invalid token.");

            await TryStampLastUsedAsync(match.Id, now);

            var scope = match.Scopes == PatScope.ReadWrite ? "read_write" : "read";
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(Constants.Claims.UserId, match.UserId),
                new Claim(Constants.Claims.Scope, scope),
                new Claim(Constants.Claims.PatId, match.Id),
            }, SchemeName);

            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
        }

        // Emit an RFC 9110 WWW-Authenticate header on the 401 so MCP (and any HTTP) clients
        // get a well-formed challenge instead of a bare 401. Minimal on purpose: "Bearer",
        // adding error="invalid_token" only when a token was actually presented and rejected
        // (AuthenticateResult.Failure is set), versus simply absent. No OAuth
        // resource-metadata / WWW-Authenticate parameters beyond that.
        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;

            var result = await HandleAuthenticateOnceAsync();
            var challenge = result?.Failure is not null
                ? "Bearer error=\"invalid_token\""
                : "Bearer";
            Response.Headers.WWWAuthenticate = challenge;
        }

        // Best-effort LastUsedAt stamp: must never fail the request. Written through a fresh
        // factory-created context, NOT the shared request-scoped `db`: this keeps the stamp
        // fully isolated from the rest of the request — no tracked state leaks onto the shared
        // context (so no detach dance), and its SaveChanges can't flush unrelated pending
        // changes on `db`. A keyed stub with only LastUsedAt marked modified avoids reading the
        // row and avoids clobbering its other columns.
        private async Task TryStampLastUsedAsync(string id, DateTimeOffset now)
        {
            try
            {
                await using var stampDb = await dbFactory.CreateDbContextAsync();
                var stub = new PersonalAccessToken { Id = id };
                stampDb.PersonalAccessTokens.Attach(stub);
                stub.LastUsedAt = now;
                await stampDb.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to update PAT LastUsedAt.");
            }
        }
    }
}
