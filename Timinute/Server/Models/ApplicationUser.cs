using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Timinute.Server.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(50)]
        [PersonalData]
        public string FirstName { get; set; } = null!;

        [Required, MaxLength(50)]
        [PersonalData]
        public string LastName { get; set; } = null!;

        public DateTimeOffset? LastLoginDate { get; set; }

        public ICollection<TrackedTask>? TrackedTasks { get; set; }

        public ICollection<Project>? Projects { get; set; }
    }
}