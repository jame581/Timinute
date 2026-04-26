using System.Net.Http.Json;

namespace Timinute.Client.Helpers
{
    public static class PagingExtensions
    {
        // Server caps PageSize at 50 (PagingParameters.maxPageSize).
        private const int PageSize = 50;

        /// <summary>
        /// Fetches every page from a paged endpoint and returns the combined list.
        /// Stops when a returned page is shorter than <paramref name="pageSize"/> or empty.
        /// Honors any existing query string on the URL.
        /// </summary>
        public static async Task<List<T>> GetAllPagedAsync<T>(this HttpClient client, string baseUrl, int pageSize = PageSize)
        {
            var separator = baseUrl.Contains('?') ? "&" : "?";
            var results = new List<T>();
            var page = 1;

            while (true)
            {
                var url = $"{baseUrl}{separator}PageNumber={page}&PageSize={pageSize}";
                var batch = await client.GetFromJsonAsync<List<T>>(url) ?? new();

                if (batch.Count == 0)
                    break;

                results.AddRange(batch);

                if (batch.Count < pageSize)
                    break;

                page++;
            }

            return results;
        }
    }
}
