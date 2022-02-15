namespace Timinute.Client.Helpers
{
    public static class Constants
    {
        public static class Paging
        {
            public const string PagingHeader = "X-Pagination";
        }

        public static class API
        {
            public const string ClientName = "Timinute.ServerAPI";

            public static class TrackedTask
            {
                public const string Api = "TrackedTask";

                public const string Get = "TrackedTask";

                //public const string GetPaged = "TrackedTask/PageNumber={0}&OrderBy={1}&Filter={2}&PageSize={3}";

                public const string GetById = $"{Api}/";

                public const string Create = Api;

                public const string Delete = Api;

                public const string Update = Api;
            }

            public static class Project
            {
                public const string Api = "Project";

                public const string GetAll = "Project";

                public const string GetById = $"{Api}/";

                public const string Create = Api;

                public const string Delete = Api;

                public const string Update = Api;
            }

            public static class Analytics
            {
                public const string Api = "Analytics";

                public const string GetProjectWorkTime = $"{Api}/ProjectWorkTime";

                public const string GetProjectWorkTimePerMonths = $"{Api}/ProjectWorkTimePerMonths";

                public const string GetWorkTimePerMonths = $"{Api}/WorkTimePerMonths";

                public const string GetAmountWorkTimeLastMonth = $"{Api}/AmountWorkTimeLastMonth";

                public const string GetById = $"{Api}/";
            }
        }
    }
}
