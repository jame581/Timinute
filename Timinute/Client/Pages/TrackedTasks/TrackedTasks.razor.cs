using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;
using System.Text.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.Paging;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Pages.TrackedTasks
{
    public partial class TrackedTasks
    {
        private IList<TrackedTask> trackedTasksList = new List<TrackedTask>();

        private readonly IList<int> pageSizes = new List<int> { 10, 25, 50 };

        private int tasksCount = 0;

        private int totalCount = 0;

        private int totalPageCount = 1;

        private int pageSize = 10;

        private int currentPage = 1;

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

            await LoadPage(new LoadDataArgs { Top = pageSizes[0] });
            await radzenDataGrid.Reload();
        }

        private async Task LoadPage(LoadDataArgs args)
        {
            isLoading = true;
            try
            {
                var requestMessage = new HttpRequestMessage()
                {
                    Method = new HttpMethod("GET"),
                    RequestUri = new Uri(ConstructUrlRequest(args)),
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

                    tasksCount = trackedTasksList.Count;

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
                totalPageCount = pagination.TotalPages;
                pageSize = pagination.PageSize;
                tasksCount = pagination.TotalCount;
                totalCount = pagination.TotalCount;
            }
        }

        async Task LoadData(LoadDataArgs args)
        {
            if (args.Top.HasValue && pageSize != args.Top.Value)
            {
                pageSize = args.Top.Value;
            }

            if (args.Skip.HasValue)
            {
                currentPage = ((int)args.Skip / pageSize) + 1;
            }

            await LoadPage(args);
            await InvokeAsync(StateHasChanged);
        }

        private async Task HandleTrackedTaskAdded(TrackedTask trackedTask)
        {
            //await RefreshTable();
            await radzenDataGrid.Reload();
        }

        private string ConstructUrlRequest(LoadDataArgs args)
        {
            string url = client.BaseAddress + Constants.API.TrackedTask.Get + $"?PageNumber={currentPage}";

            if (!string.IsNullOrEmpty(args.OrderBy))
            {
                url += $"&OrderBy={args.OrderBy}";
            }

            if (!string.IsNullOrEmpty(args.Filter))
            {
                url += $"&Filter={args.Filter}";
            }

            url += $"&PageSize={args.Top}";
            
            return url;
        }
    }
}
