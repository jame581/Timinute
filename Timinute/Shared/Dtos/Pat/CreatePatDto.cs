using System.ComponentModel.DataAnnotations;

namespace Timinute.Shared.Dtos.Pat
{
    public class CreatePatDto
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = null!;

        // Anchored: RegularExpressionAttribute requires the match to span the whole
        // value, but the regex engine still tries alternatives left-to-right within
        // that constraint - an unanchored "read|read_write" matches only the "read"
        // prefix of "read_write" and fails validation for the longer value.
        [Required, RegularExpression("^(read|read_write)$")]
        public string Scope { get; set; } = "read";

        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
