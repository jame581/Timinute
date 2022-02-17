using Radzen;

namespace Timinute.Client.Helpers
{
    public static class Constants
    {
        public static class Paging
        {
            public const string PagingHeader = "X-Pagination";

            public static readonly List<int> PageSizeOptions = new List<int> { 10, 25, 50 };

            public static string ConstructUrlTrackedTaskRequest(string clientBaseAddress, int currentPage, LoadDataArgs args)
            {
                string url = clientBaseAddress + $"?PageNumber={currentPage}";

                if (!string.IsNullOrEmpty(args.OrderBy))
                {
                    url += $"&OrderBy={args.OrderBy}";
                }

                if (!string.IsNullOrEmpty(args.Filter))
                {
                    url += $"&Filter={args.Filter}";
                }

                url += $"&PageSize={args.Top}";

                return url;
            }

            public static string ConstructUrlFromPagerRequest(string clientBaseAddress, int currentPage, PagerEventArgs args)
            {
                string url = clientBaseAddress + $"?PageNumber={currentPage}";

                url += $"&PageSize={args.Top}";

                return url;
            }
        }

        public static class API
        {
            public const string ClientName = "Timinute.ServerAPI";

            public static class TrackedTask
            {
                public const string Api = "TrackedTask";

                public const string Get = "TrackedTask";

                public const string GetById = $"{Api}/";

                public const string Create = Api;

                public const string Delete = Api;

                public const string Update = Api;
            }

            public static class Project
            {
                public const string Api = "Project";

                public const string Get = "Project";

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

                public const string GetAmountWorkTimeByMonth = $"{Api}/AmountWorkTimeByMonth";

                public const string GetById = $"{Api}/";

                public static string ConstructUrlForAmountWorkTimeByMonth(int year, int month)
                {
                    return GetAmountWorkTimeByMonth + $"?Year={year}&Month={month}";
                }
            }
        }
    }
}
