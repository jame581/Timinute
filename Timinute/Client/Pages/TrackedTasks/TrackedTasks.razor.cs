using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using Radzen.Blazor;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Pages.TrackedTasks
{
    public partial class TrackedTasks
    {
        private readonly IList<TrackedTask> trackedTasksList = new List<TrackedTask>();

        private int tasksCount = 0;

        private bool isLoading = true;

        private RadzenDataGrid<TrackedTask> radzenDataGrid = null!;

        [CascadingParameter]
        private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;

        #region Dependency Injection

        [Inject]
        protected NavigationManager Navigation { get; set; } = null!;

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        [Inject]
        private NotificationService notificationService { get; set; } = null!;
        
        #endregion

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthenticationStateTask;
            var user = authState.User;

            if (user.Identity != null && !user.Identity.IsAuthenticated)
                Navigation.NavigateTo($"{Navigation.BaseUri}auth/login", true);

            await RefreshTable();
            await radzenDataGrid.Reload();
        }

        private async Task RefreshTable()
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);
            isLoading = true;
            try
            {
                var response = await client.GetFromJsonAsync<TrackedTaskDto[]>(Constants.API.TrackedTask.GetAll);

                if (response != null)
                {
                    trackedTasksList.Clear();

                    foreach (var item in response)
                        trackedTasksList.Add(new TrackedTask(item));

                    tasksCount = trackedTasksList.Count;
                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happened", ex.Message, 5000);
            }

            isLoading = false;
        }

        async Task LoadData(LoadDataArgs args)
        {   
            await RefreshTable();   
        }

        private async Task HandleTrackedTaskAdded(TrackedTask trackedTask)
        {
            await RefreshTable();
            await radzenDataGrid.Reload();
        }
    }
}
