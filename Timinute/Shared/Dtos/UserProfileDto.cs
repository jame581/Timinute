namespace Timinute.Shared.Dtos
{
    public class UserProfileDto
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public TimeSpan TotalTrackedTime { get; set; }
        public int ProjectCount { get; set; }
        public int TaskCount { get; set; }
        public UserPreferencesDto Preferences { get; set; } = new();
    }
}
