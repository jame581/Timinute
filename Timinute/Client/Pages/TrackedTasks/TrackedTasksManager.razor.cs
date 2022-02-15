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
    public partial class TrackedTasksManager
    {
        private List<TrackedTask> trackedTasksList { get; set; } = new();

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

            try
            {
                var response = await client.GetFromJsonAsync<TrackedTaskDto[]>(Constants.API.TrackedTask.Get);

                if (response != null)
                {
                    trackedTasksList.Clear();

                    foreach (var item in response)
                        trackedTasksList.Add(new TrackedTask(item));

                    tasksCount = trackedTasksList.Count;
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Validation error", ex.Message, 5000);
            }
        }

        async Task LoadData(LoadDataArgs args)
        {
            isLoading = true;

            await RefreshTable();

            isLoading = false;
        }

        private async Task RemoveTrackedTask(string trackedTaskId)
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var response = await client.DeleteAsync($"{Constants.API.TrackedTask.Delete}/{trackedTaskId}");

                if (response != null && response.IsSuccessStatusCode)
                {
                    await RefreshTable();
                    notificationService.Notify(NotificationSeverity.Success, "Success", "Tracked task was removed", 3000);
                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Validation error", ex.Message, 5000);
            }
        }
    }
}
