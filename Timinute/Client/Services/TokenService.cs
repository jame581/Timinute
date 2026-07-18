using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Timinute.Client.Helpers;
using Timinute.Shared.Dtos.Pat;

namespace Timinute.Client.Services
{
    // Thin typed wrapper around GET/POST/DELETE /Pat. Unlike UserProfileService /
    // AnalyticsService this deliberately does NOT cache the list: tokens are only
    // read from the Settings > Tokens page, so there is no cross-page consumer to
    // keep warm, and every mutation (create/revoke) would need to invalidate it
    // anyway - a plain pass-through avoids that machinery for no benefit.
    public class TokenService
    {
        private readonly IHttpClientFactory clientFactory;

        public TokenService(IHttpClientFactory clientFactory)
        {
            this.clientFactory = clientFactory;
        }

        private HttpClient Http => clientFactory.CreateClient(Constants.API.ClientName);

        public async Task<List<PersonalAccessTokenDto>> ListAsync()
            => await Http.GetFromJsonAsync<List<PersonalAccessTokenDto>>("Pat") ?? new();

        public async Task<CreatedPatDto?> CreateAsync(CreatePatDto dto)
        {
            var response = await Http.PostAsJsonAsync("Pat", dto);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CreatedPatDto>();
        }

        public async Task RevokeAsync(string id)
        {
            var response = await Http.DeleteAsync($"Pat/{id}");
            response.EnsureSuccessStatusCode();
        }
    }
}
