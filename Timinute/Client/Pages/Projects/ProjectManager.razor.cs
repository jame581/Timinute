using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.Project;

namespace Timinute.Client.Pages.Projects
{
    public partial class ProjectManager
    {
        private List<Project> ProjectsList { get; set; } = new();

        private string ExceptionMessage { get; set; } = "";

        [CascadingParameter]
        private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;

        [Inject]
        protected NavigationManager Navigation { get; set; } = null!;

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthenticationStateTask;
            var user = authState.User;

            if (user.Identity != null && !user.Identity.IsAuthenticated)
                Navigation.NavigateTo($"{Navigation.BaseUri}auth/login", true);

            await RefreshTable();
        }

        private async Task RefreshTable()
        {
            ExceptionMessage = "";
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var response = await client.GetFromJsonAsync<ProjectDto[]>(Constants.API.Project.GetAll);

                if (response != null)
                {
                    ProjectsList.Clear();

                    foreach (var item in response)
                        ProjectsList.Add(new Project(item));
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                ExceptionMessage = ex.Message;
            }
        }

        private async Task HandleProjectAdded(Project project)
        {
            await RefreshTable();
        }

        private async Task RemoveProject(string projectId)
        {
            ExceptionMessage = "";
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var response = await client.DeleteAsync($"{Constants.API.Project.Delete}/{projectId}");

                if (response != null && response.IsSuccessStatusCode)
                {
                    await RefreshTable();
                }
            }
            catch (Exception ex)
            {
                ExceptionMessage = ex.Message;
            }
        }
    }
}
