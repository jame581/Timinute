using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Timinute.Client.Components.TrackedTasks;
using Timinute.Client.Models;

namespace Timinute.Client.Pages
{
    public partial class TimeTracker
    {
        private TrackedTaskTable trackedTaskTableComponent = null!;
        
        [CascadingParameter]
        private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;

        [Inject]
        protected NavigationManager Navigation { get; set; } = null!;

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthenticationStateTask;
            var user = authState.User;

            if (user.Identity != null && !user.Identity.IsAuthenticated)
                Navigation.NavigateTo($"{Navigation.BaseUri}auth/login", true);
        }

        private async Task HandleTrackedTaskAdded(TrackedTask newTrackedTask)
        {
           await trackedTaskTableComponent.RefreshTable();
        }
    }
}
