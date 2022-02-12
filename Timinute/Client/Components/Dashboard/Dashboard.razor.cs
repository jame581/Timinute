using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Shared.Dtos.Dashboard;

namespace Timinute.Client.Components.Dashboard
{
    public partial class Dashboard
    {
        [CascadingParameter]
        private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;

        private string AmountWorkTimeLastMonth = "00:00:00";

        private string TopProjectLastMonth = "None - 00:00:00";

        [Inject]
        protected NavigationManager Navigation { get; set; } = null!;

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        public System.Security.Claims.ClaimsPrincipal User { get; set; }


        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthenticationStateTask;
            User = authState.User;

            if (User.Identity != null && !User.Identity.IsAuthenticated)
                Navigation.NavigateTo($"{Navigation.BaseUri}auth/login", true);

            await LoadAmountWorkTimeLastMonth();
        }

        private async Task LoadAmountWorkTimeLastMonth()
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var response = await client.GetFromJsonAsync<AmountOfWorkTimeDto>(Constants.API.Analytics.GetAmountWorkTimeLastMonth);

                if (response != null)
                {
                    AmountWorkTimeLastMonth = response.AmountWorkTimeText;
                    TopProjectLastMonth = $"{response.TopProject} - {response.TopProjectAmounTimeText}";
                }
            }
            catch (Exception ex)
            {
                // TODO(jame_581): Add notification
            }

            StateHasChanged();
        }
    }
}
