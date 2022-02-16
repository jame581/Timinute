using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;
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
        private IList<TrackedTask> trackedTasksList = new List<TrackedTask>();

        PagingAttributes pagingAttributes = new PagingAttributes();

        private bool isLoading = true;

        private RadzenDataGrid<TrackedTask> radzenDataGrid = null!;

        private HttpClient client;

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

        private async Task HandleTrackedTaskAdded(TrackedTask trackedTask)
        {
            //await RefreshTable();
            await radzenDataGrid.Reload();
        }
    }
}
