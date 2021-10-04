namespace Timinute.Client.Helpers
{
    public static class Constants
    {
        public static class API
        {
            public const string ClientName = "Timinute.ServerAPI";

            public static class TrackedTask
            {                
                public const string Api = "TrackedTask";

                public const string GetAll = "TrackedTasks";

                public const string GetById = $"{Api}/";

                public const string Create = Api;

                public const string Delete = Api;

                public const string Update = Api;
            }           
        }
    }
}
