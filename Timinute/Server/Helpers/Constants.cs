namespace Timinute.Server.Helpers
{
    public static class Constants
    {
        public static class Roles
        {
            public const string Admin = "Admin";
            public const string Basic = "Basic";
        }

        public static class Claims
        {
            public const string UserId = "sub";
            public const string LastLogin = "LastLogin";
            public const string Fullname = "FullName";
            public const string Role = "Role";

        }
    }
}
