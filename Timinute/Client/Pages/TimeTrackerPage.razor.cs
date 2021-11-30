using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Pages
{
    public partial class TimeTrackerPage
    {
        [CascadingParameter]
        private Task<AuthenticationState> authenticationStateTask { get; set; }

        [Inject]
        protected NavigationManager Navigation { get; set; }

        [Inject]
        private IHttpClientFactory clientFactory { get; set; }

        //private EditContext? editContext;

        private CreateTrackedTaskDto createTrackedTask;

        protected override async Task OnInitializedAsync()
        {
            var authState = await authenticationStateTask;
            var user = authState.User;

            if (user.Identity != null && !user.Identity.IsAuthenticated)
                Navigation.NavigateTo($"{Navigation.BaseUri}auth/login", true);

            //editContext = new(createTrackedTask);
        }

        //private async Task HandleSubmit()
        //{
        //    if (editContext != null && editContext.Validate())
        //    {
        //        //Logger.LogInformation("HandleSubmit called: Form is valid");

        //        // Process the valid form
        //        // await ...
        //        await Task.CompletedTask;
        //    }
        //    else
        //    {
        //        //Logger.LogInformation("HandleSubmit called: Form is INVALID");
        //    }
        //}
    }
}
