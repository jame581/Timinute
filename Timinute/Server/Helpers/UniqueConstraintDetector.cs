using Microsoft.EntityFrameworkCore;

namespace Timinute.Server.Helpers
{
    /// <summary>
    /// Provider-agnostic detection of a unique-constraint / duplicate-key violation
    /// surfaced through a <see cref="DbUpdateException"/>. Single definition shared by
    /// the controllers and the app-services so the detection can never drift between them.
    /// </summary>
    public static class UniqueConstraintDetector
    {
        public static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
        }
    }
}
