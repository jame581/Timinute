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
        private List<Project> projectsList { get; set; } = new();

        private string exceptionMessage { get; set; }

        [CascadingParameter]
        private Task<AuthenticationState> authenticationStateTask { get; set; }

        [Inject]
        protected NavigationManager Navigation { get; set; }

        [Inject]
        private IHttpClientFactory clientFactory { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var authState = await authenticationStateTask;
            var user = authState.User;

            if (user.Identity != null && !user.Identity.IsAuthenticated)
                Navigation.NavigateTo($"{Navigation.BaseUri}auth/login", true);

            await RefreshTable();
        }

        private async Task RefreshTable()
        {
            exceptionMessage = "";
            var client = clientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var response = await client.GetFromJsonAsync<ProjectDto[]>(Constants.API.Project.GetAll);

                if (response != null)
                {
                    projectsList.Clear();

                    foreach (var item in response)
                        projectsList.Add(new Project(item));
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                exceptionMessage = ex.Message;
            }
        }

        private async Task HandleProjectAdded(Project project)
        {
            await RefreshTable();
        }

        private async Task RemoveProject(string projectId)
        {
            exceptionMessage = "";
            var client = clientFactory.CreateClient(Constants.API.ClientName);

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
                exceptionMessage = ex.Message;
            }
        }
    }
}
