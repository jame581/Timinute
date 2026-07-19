using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Timinute.Client.Helpers;
using Timinute.Shared.Dtos.Mcp;

namespace Timinute.Client.Services
{
    // Thin typed wrapper around GET /Pat/activity, mirroring TokenService: a single
    // pass-through read, no caching. The audit trail is only read from the Settings >
    // AI activity page, so there is no cross-page consumer to keep warm.
    public class McpActivityService
    {
        private readonly IHttpClientFactory clientFactory;

        public McpActivityService(IHttpClientFactory clientFactory)
        {
            this.clientFactory = clientFactory;
        }

        private HttpClient Http => clientFactory.CreateClient(Constants.API.ClientName);

        public async Task<List<McpActivityDto>> ListAsync()
            => await Http.GetFromJsonAsync<List<McpActivityDto>>("Pat/activity") ?? new();
    }
}
