using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Timinute.Client.Components;
using Timinute.Client.Models;

namespace Timinute.Client.Pages
{
    public partial class TimeTrackerPage
    {
        [CascadingParameter]
        private Task<AuthenticationState> authenticationStateTask { get; set; }

        private TrackedTaskTable trackedTaskTableComponent;

        [Inject]
        protected NavigationManager Navigation { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var authState = await authenticationStateTask;
            var user = authState.User;

            if (user.Identity != null && !user.Identity.IsAuthenticated)
                Navigation.NavigateTo($"{Navigation.BaseUri}auth/login", true);
        }

        private async Task HandleTrackedTaskAdded(TrackedTask trackedTaskDto)
        {
           await trackedTaskTableComponent.RefreshTable();
        }
    }
}
