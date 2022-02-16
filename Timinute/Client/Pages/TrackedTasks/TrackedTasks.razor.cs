using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;
using System.Net.Http.Json;
using System.Text.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Client.Models.Paging;
using Timinute.Shared.Dtos.Paging;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Pages.TrackedTasks
{
    public partial class TrackedTasks
    {
        IList<TrackedTask> trackedTasksList = new List<TrackedTask>();

        PagingAttributes pagingAttributes = new PagingAttributes();

        bool isLoading = true;

        RadzenDataGrid<TrackedTask> radzenDataGrid = null!;

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
                            client.BaseAddress!.ToString() + Constants.API.TrackedTask.Get,
                            pagingAttributes.CurrentPage,
                            args)),
                };

                requestMessage.Headers.Add("accept", "application/json");

                var response = await client.SendAsync(requestMessage);

                if (response != null && response.IsSuccessStatusCode)
                {
                    trackedTasksList = new List<TrackedTask>();

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    DeserializeAndSetupPagingInfo(response.Headers.GetValues(Constants.Paging.PagingHeader).FirstOrDefault(), options);

                    var stringData = await response.Content.ReadAsStringAsync();
                    var trackedTaskResponse = JsonSerializer.Deserialize<TrackedTaskDto[]>(stringData, options);

                    if (trackedTaskResponse != null)
                    {
                        foreach (var item in trackedTaskResponse)
                            trackedTasksList.Add(new TrackedTask(item));
                    }

                    pagingAttributes.Count = trackedTasksList.Count;

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

        async Task EditRow(TrackedTask trackedTask)
        {
            await radzenDataGrid.EditRow(trackedTask);
        }

        async Task SaveRow(TrackedTask trackedTask)
        {
            await radzenDataGrid.UpdateRow(trackedTask);
        }

        void CancelEdit(TrackedTask trackedTask)
        {
            radzenDataGrid.CancelEditRow(trackedTask);
        }

        async Task DeleteRow(TrackedTask trackedTask)
        {
            try
            {
                var response = await client.DeleteAsync($"{Constants.API.TrackedTask.Delete}/{trackedTask.TaskId}");

                if (response != null && response.IsSuccessStatusCode)
                {
                    await radzenDataGrid.Reload();
                    notificationService.Notify(NotificationSeverity.Success, "Success", "Tracked Task was removed", 3000);
                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Validation error", ex.Message, 5000);
            }
        }

        private async Task HandleTrackedTaskAdded(TrackedTask trackedTask)
        {
            //await RefreshTable();
            await radzenDataGrid.Reload();
        }

        async Task OnTrackedTaskUpdate(TrackedTask trackedTask)
        {
            UpdateTrackedTaskDto updateDto = new()
            {
                TaskId = trackedTask.TaskId,
                Name = trackedTask.Name,
                StartDate = trackedTask.StartDate,
                EndDate = trackedTask.EndDate,
                ProjectId = trackedTask.ProjectId,
            };

            try
            {
                var responseMessage = await client.PutAsJsonAsync(Constants.API.TrackedTask.Update, updateDto);
                responseMessage.EnsureSuccessStatusCode();

                if (trackedTask.EndDate.HasValue)
                {
                    trackedTask.Duration = trackedTask.EndDate.Value - trackedTask.StartDate;
                }

                notificationService.Notify(NotificationSeverity.Success, "Tracked task updated", "Tracked task was updated successfully.", 5000);
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Validation error", ex.Message, 5000);
            }
        }

        async Task OnTrackedTaskCreate(TrackedTask trackedTask)
        {
            CreateTrackedTaskDto createDto = new()
            {
                Name = trackedTask.Name,
                StartDate = trackedTask.StartDate,
                Duration = trackedTask.EndDate.HasValue ? (trackedTask.EndDate - trackedTask.StartDate).Value : trackedTask.Duration,
                ProjectId = trackedTask.ProjectId,
            };

            try
            {
                var responseMessage = await client.PostAsJsonAsync(Constants.API.TrackedTask.Create, createDto);
                responseMessage.EnsureSuccessStatusCode();
                notificationService.Notify(NotificationSeverity.Success, "Tracked Task created", "New Tracked Task created successfully.", 5000);
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Validation error", ex.Message, 5000);
            }
        }
    }
}
