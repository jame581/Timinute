using Microsoft.AspNetCore.Identity;

namespace Timinute.Server.Models
{
    public class ApplicationRole : IdentityRole
    {
        public string Description { get; set; } = null!;
    }
}
