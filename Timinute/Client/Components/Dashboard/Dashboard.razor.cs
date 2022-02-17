using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using System.Net.Http.Json;
using System.Security.Claims;
using Timinute.Client.Helpers;
using Timinute.Shared.Dtos.Dashboard;

namespace Timinute.Client.Components.Dashboard
{
    public partial class Dashboard
    {
        public ClaimsPrincipal User { get; set; }

        private string AmountWorkTimeLastMonth = "00:00:00";

        private string TopProjectLastMonth = "None - 00:00:00";

        private string AmountWorkTimeThisMonth = "00:00:00";

        private string TopProjectThisMonth = "None - 00:00:00";

        #region Dependency Injection

        [Inject]
        protected NavigationManager Navigation { get; set; } = null!;

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        [Inject]
        private NotificationService notificationService { get; set; } = null!;

        #endregion

        [CascadingParameter]
        private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthenticationStateTask;
            User = authState.User;

            if (User.Identity != null && !User.Identity.IsAuthenticated)
                Navigation.NavigateTo($"{Navigation.BaseUri}auth/login", true);

            await LoadAmountWorkTimeLastMonth();
            await LoadAmountWorkTimeActualMonth();
        }

        private async Task LoadAmountWorkTimeLastMonth()
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                DateTime lastMonth = DateTime.Now.AddMonths(-1);
                var response = await client.GetFromJsonAsync<AmountOfWorkTimeDto>(Constants.API.Analytics.ConstructUrlForAmountWorkTimeByMonth(lastMonth.Year, lastMonth.Month));

                if (response != null)
                {
                    AmountWorkTimeLastMonth = response.AmountWorkTimeText;
                    TopProjectLastMonth = $"{response.TopProject} - {response.TopProjectAmounTimeText}";
                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happened", ex.Message, 5000);
            }

            StateHasChanged();
        }

        private async Task LoadAmountWorkTimeActualMonth()
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                DateTime now = DateTime.Now;
                var response = await client.GetFromJsonAsync<AmountOfWorkTimeDto>(Constants.API.Analytics.ConstructUrlForAmountWorkTimeByMonth(now.Year, now.Month));

                if (response != null)
                {
                    AmountWorkTimeThisMonth = response.AmountWorkTimeText;
                    TopProjectThisMonth = $"{response.TopProject} - {response.TopProjectAmounTimeText}";
                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happened", ex.Message, 5000);
            }

            StateHasChanged();
        }
    }
}
