using System.Security.Claims;
using Timinute.Server.Helpers;

namespace Timinute.Server.Mcp
{
    /// <summary>
    /// Resolves the calling user's identity and scope from the PAT principal that
    /// <c>MapMcp("/mcp").RequireAuthorization(...)</c> attached to the current HTTP request.
    /// Registered scoped; each MCP tool instance is constructed per call inside the same
    /// per-request DI scope (spike fact (b)), so this reads the live PAT claims for that
    /// call. Never cache <see cref="UserId"/> across calls — resolve it per invocation.
    /// </summary>
    public sealed class McpUserContext
    {
        private readonly IHttpContextAccessor accessor;

        public McpUserContext(IHttpContextAccessor accessor) => this.accessor = accessor;

        public string UserId => accessor.HttpContext?.User.FindFirstValue(Constants.Claims.UserId)
            ?? throw new UnauthorizedAccessException("No PAT user.");

        public bool CanWrite =>
            accessor.HttpContext?.User.FindFirstValue(Constants.Claims.Scope) == "read_write";

        public string? TokenId => accessor.HttpContext?.User.FindFirstValue(Constants.Claims.PatId);

        /// <summary>
        /// Defense-in-depth scope gate called first by every write tool. The authoritative
        /// central call-tool filter arrives in Task 8; this keeps writes safe in the interim.
        /// </summary>
        public void RequireWrite()
        {
            if (!CanWrite) throw new UnauthorizedAccessException("This token is read-only.");
        }
    }
}
