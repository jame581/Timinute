using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using Radzen.Blazor;
using System.Net.Http.Json;
using System.Text.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Client.Models.Paging;
using Timinute.Shared.Dtos.Paging;
using Timinute.Shared.Dtos.Project;

namespace Timinute.Client.Pages.Projects
{
    public partial class ProjectManager
    {
        List<Project> projectsList { get; set; } = new();

        PagingAttributes pagingAttributes = new PagingAttributes();

        bool isLoading = true;

        RadzenDataGrid<Project> radzenDataGrid = null!;

        HttpClient client;

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

            client = ClientFactory.CreateClient(Constants.API.ClientName);

            await LoadPage(new LoadDataArgs { Top = Constants.Paging.PageSizeOptions[0] });
        }

        private async Task LoadPage(LoadDataArgs args)
        {
            isLoading = true;
            try
            {
                var requestMessage = new HttpRequestMessage()
                {
                    Method = new HttpMethod("GET"),
                    RequestUri = new Uri(
                        Constants.Paging.ConstructUrlTrackedTaskRequest(
                            client.BaseAddress!.ToString() + Constants.API.Project.Get,
                            pagingAttributes.CurrentPage,
                            args)),
                };

                requestMessage.Headers.Add("accept", "application/json");

                var response = await client.SendAsync(requestMessage);

                if (response != null && response.IsSuccessStatusCode)
                {
                    projectsList = new List<Project>();

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    DeserializeAndSetupPagingInfo(response.Headers.GetValues(Constants.Paging.PagingHeader).FirstOrDefault(), options);

                    var stringData = await response.Content.ReadAsStringAsync();
                    var projectsResponse = JsonSerializer.Deserialize<ProjectDto[]>(stringData, options);

                    if (projectsResponse != null)
                    {
                        foreach (var item in projectsResponse)
                            projectsList.Add(new Project(item));
                    }

                    pagingAttributes.Count = projectsList.Count;
                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happened", ex.Message, 5000);
            }

            isLoading = false;

            await InvokeAsync(StateHasChanged);
        }

        private void DeserializeAndSetupPagingInfo(string? paginationValues, JsonSerializerOptions options)
        {
            if (paginationValues == null) return;

            var pagination = JsonSerializer.Deserialize<PaginationHeaderDto>(paginationValues, options);
            if (pagination != null)
            {
                pagingAttributes.TotalPageCount = pagination.TotalPages;
                pagingAttributes.PageSize = pagination.PageSize;
                pagingAttributes.Count = pagination.TotalCount;
                pagingAttributes.TotalCount = pagination.TotalCount;
            }
        }

        async Task LoadData(LoadDataArgs args)
        {
            if (args.Top.HasValue && pagingAttributes.PageSize != args.Top.Value)
            {
                pagingAttributes.PageSize = args.Top.Value;
            }

            if (args.Skip.HasValue)
            {
                pagingAttributes.CurrentPage = ((int)args.Skip / pagingAttributes.PageSize) + 1;
            }

            await LoadPage(args);
            await InvokeAsync(StateHasChanged);
        }

        private async Task HandleProjectAdded(Project project)
        {
            await radzenDataGrid.Reload();
        }

        async Task EditRow(Project project)
        {
            await radzenDataGrid.EditRow(project);
        }

        async Task SaveRow(Project project)
        {
            await radzenDataGrid.UpdateRow(project);
        }

        void CancelEdit(Project project)
        {
            radzenDataGrid.CancelEditRow(project);
        }

        private async Task RemoveProject(Project project)
        {
            try
            {
                var response = await client.DeleteAsync($"{Constants.API.Project.Delete}/{project.ProjectId}");

                if (response != null && response.IsSuccessStatusCode)
                {
                    await radzenDataGrid.Reload();
                    notificationService.Notify(NotificationSeverity.Success, "Success", "Project was removed", 3000);
                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Validation error", ex.Message, 5000);
            }
        }

        async Task OnProjectUpdate(Project project)
        {
            UpdateProjectDto updateProjectDto = new()
            {
                ProjectId = project.ProjectId,
                Name = project.Name
            };

            try
            {
                var responseMessage = await client.PutAsJsonAsync(Constants.API.Project.Update, updateProjectDto);
                responseMessage.EnsureSuccessStatusCode();

                notificationService.Notify(NotificationSeverity.Success, "Project updated", "Project was updated successfully.", 5000);
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Validation error", ex.Message, 5000);
            }
        }

        async Task OnProjectCreate(Project project)
        {
            ProjectDto createProjectDto = new()
            {
                Name = project.Name
            };

            try
            {
                var responseMessage = await client.PostAsJsonAsync(Constants.API.Project.Create, createProjectDto);
                responseMessage.EnsureSuccessStatusCode();
                notificationService.Notify(NotificationSeverity.Success, "Project created", "New project created successfully.", 5000);
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Validation error", ex.Message, 5000);
            }
        }
    }
}
