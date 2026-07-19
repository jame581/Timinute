namespace Timinute.Server.Models
{
    public enum PatScope { Read, ReadWrite }

    public class PersonalAccessToken
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = null!;
        public ApplicationUser? User { get; set; }

        public string Name { get; set; } = null!;
        public string TokenHash { get; set; } = null!;   // SHA-256 hex of the full token
        public string Prefix { get; set; } = null!;       // first 8 chars, plaintext, for display + lookup
        public PatScope Scopes { get; set; } = PatScope.Read;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? LastUsedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
    }
}
