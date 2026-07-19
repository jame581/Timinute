namespace Timinute.Shared.Dtos.Pat
{
    public class PersonalAccessTokenDto
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Prefix { get; set; } = null!;
        public string Scope { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? LastUsedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
