namespace Timinute.Server.Helpers
{
    public static partial class Constants
    {
        public static class Api
        {
            // JWT audience, IdentityServer ApiScope/ApiResource, and OIDC scope name.
            public const string ResourceName = "Timinute.ServerAPI";

            // Development fallback when IdentityServer:Authority is unset.
            public const string DefaultAuthority = "https://localhost:7047";
        }

        public static class CacheProfiles
        {
            public const string Default120 = "Default120";
        }
    }
}
