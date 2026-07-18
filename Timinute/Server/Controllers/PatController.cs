using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Server.Services.Pat;
using Timinute.Shared.Dtos.Pat;

namespace Timinute.Server.Controllers
{
    // Reachable only through the cookie/JWT policy scheme ("ApplicationDefinedPolicy" in
    // Program.cs) - the "Pat" scheme is registered but deliberately not wired into
    // ForwardDefaultSelector, so a PAT can never authenticate to this controller.
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class PatController : ControllerBase
    {
        private readonly IRepository<PersonalAccessToken> repository;
        private readonly IPatTokenService tokenService;
        private readonly ILogger<PatController> logger;

        public PatController(IRepositoryFactory repositoryFactory, IPatTokenService tokenService, ILogger<PatController> logger)
        {
            repository = repositoryFactory.GetRepository<PersonalAccessToken>();
            this.tokenService = tokenService;
            this.logger = logger;
        }

        // GET: /Pat
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PersonalAccessTokenDto>>> GetPersonalAccessTokens()
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var tokens = await repository.Get(t => t.UserId == userId && t.RevokedAt == null);

            return Ok(tokens.Select(ToDto));
        }

        // CREATE: /Pat
        [HttpPost]
        public async Task<ActionResult<CreatedPatDto>> CreatePersonalAccessToken([FromBody] CreatePatDto dto)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var (plaintext, hash, prefix) = tokenService.Generate();

            var newToken = new PersonalAccessToken
            {
                UserId = userId,
                Name = dto.Name.Trim(),
                TokenHash = hash,
                Prefix = prefix,
                Scopes = dto.Scope == "read_write" ? PatScope.ReadWrite : PatScope.Read,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = dto.ExpiresAt
            };

            await repository.Insert(newToken);

            logger.LogInformation("Personal access token {Prefix} ({Scope}) created for user {UserId}.", prefix, newToken.Scopes, userId);

            return Ok(new CreatedPatDto
            {
                Id = newToken.Id,
                Token = plaintext,
                Prefix = prefix,
                Scope = dto.Scope
            });
        }

        // DELETE: /Pat/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> RevokePersonalAccessToken(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // PersonalAccessToken does NOT implement ISoftDeletable and has no EF global
            // query filter, so repository.GetById happily returns an already-revoked row -
            // unlike the soft-delete entities, there is no filter safety net. Any future
            // action added here (or a new repository read of PersonalAccessToken) must repeat
            // the RevokedAt/ExpiresAt check manually; it will not be enforced for you.
            var token = await repository.GetById(id);
            if (token == null || token.UserId != userId || token.RevokedAt != null)
            {
                return NotFound("Personal access token not found!");
            }

            token.RevokedAt = DateTimeOffset.UtcNow;
            await repository.Update(token);

            logger.LogInformation("Personal access token {Prefix} revoked for user {UserId}.", token.Prefix, userId);

            return NoContent();
        }

        private static PersonalAccessTokenDto ToDto(PersonalAccessToken token) => new()
        {
            Id = token.Id,
            Name = token.Name,
            Prefix = token.Prefix,
            Scope = token.Scopes == PatScope.ReadWrite ? "read_write" : "read",
            CreatedAt = token.CreatedAt,
            LastUsedAt = token.LastUsedAt,
            ExpiresAt = token.ExpiresAt
        };
    }
}
