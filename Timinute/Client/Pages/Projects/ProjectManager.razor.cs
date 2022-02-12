using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using Radzen.Blazor;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.Project;

namespace Timinute.Client.Pages.Projects
{
    public partial class ProjectManager
    {
        private List<Project> ProjectsList { get; set; } = new();

        private int projectCount = 0;

        private bool isLoading = true;

        private RadzenDataGrid<Project> radzenDataGrid = null!;

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
                var response = await client.GetFromJsonAsync<ProjectDto[]>(Constants.API.Project.GetAll);

                if (response != null)
                {
                    ProjectsList.Clear();

                    foreach (var item in response)
                        ProjectsList.Add(new Project(item));

                    projectCount = ProjectsList.Count;
                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Validation error", ex.Message, 5000);
            }

            isLoading = false;
            StateHasChanged();
        }

        async Task LoadData(LoadDataArgs args)
        {
            isLoading = true;
            await RefreshTable();
            isLoading = false;
        }

        private async Task HandleProjectAdded(Project project)
        {
            await RefreshTable();
        }

        private async Task RemoveProject(string projectId)
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var response = await client.DeleteAsync($"{Constants.API.Project.Delete}/{projectId}");

                if (response != null && response.IsSuccessStatusCode)
                {
                    await RefreshTable();
                    notificationService.Notify(NotificationSeverity.Success, "Success", "Project was removed", 3000);
                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Validation error", ex.Message, 5000);
            }
        }
    }
}
