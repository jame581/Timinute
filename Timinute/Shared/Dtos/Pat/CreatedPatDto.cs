namespace Timinute.Shared.Dtos.Pat
{
    public class CreatedPatDto
    {
        public string Id { get; set; } = null!;
        public string Token { get; set; } = null!;   // plaintext, shown once
        public string Prefix { get; set; } = null!;
        public string Scope { get; set; } = null!;
    }
}
