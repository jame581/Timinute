using Microsoft.AspNetCore.Components;
using Radzen;
using System.Text.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.Paging;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Components.TrackedTasks
{
    public partial class TrackedTaskTable
    {
        public readonly Dictionary<string, List<TrackedTask>> trackedTasksDictionary = new();

        string pagingSummaryFormat = "Displaying page {0} of {1} (total {2} records)";

        int totalPageCount = 1;

        int tasksCount = 0;

        int totalCount = 0;

        int pageCount = 1;

        int pageSize = 10;

        int currentPage = 1;

        #region Dependency Injection

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        [Inject]
        private NotificationService notificationService { get; set; } = null!;

        #endregion

        protected override async Task OnInitializedAsync()
        {
            await LoadTrackedTasks(new PagerEventArgs { PageIndex = 1, Skip = 0, Top = 10 });
        }

        private async Task LoadTrackedTasks(PagerEventArgs args)
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var requestMessage = new HttpRequestMessage()
                {
                    Method = new HttpMethod("GET"),
                    RequestUri = new Uri(
                        Constants.Paging.ConstructUrlFromPagerRequest(
                            client.BaseAddress!.ToString() + Constants.API.TrackedTask.Get,
                            currentPage,
                            args)),
                };
                requestMessage.Headers.Add("accept", "application/json");

                var response = await client.SendAsync(requestMessage);

                if (response != null && response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    DeserializeAndSetupPagingInfo(response.Headers.GetValues(Constants.Paging.PagingHeader).FirstOrDefault(), options);

                    var stringData = await response.Content.ReadAsStringAsync();
                    var trackedTaskResponse = JsonSerializer.Deserialize<TrackedTaskDto[]>(stringData, options);

                    if (trackedTaskResponse != null)
                    {
                        List<TrackedTask> trackedTaskList = new();

                        foreach (var trackedTaskDto in trackedTaskResponse)
                            trackedTaskList.Add(new TrackedTask(trackedTaskDto));

                        GroupTraskedTasksByDay(trackedTaskList);

                        tasksCount = trackedTaskList.Count;
                    }

                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happened", ex.Message, 5000);
            }

            await InvokeAsync(StateHasChanged);
        }

        async void PageChanged(PagerEventArgs args)
        {
            currentPage = ((int)args.Skip / pageSize) + 1;
            pageSize = args.Top;

            await LoadTrackedTasks(args);
        }

        private void GroupTraskedTasksByDay(List<TrackedTask> trackedTaskList)
        {
            trackedTasksDictionary.Clear();
            var groups = trackedTaskList.
                GroupBy(x => x.StartDate.ToLongDateString())
                .OrderByDescending(groups => DateTime.Parse(groups.Key))
                .ToDictionary(x => x.Key, y => y.ToList());

            foreach (var group in groups)
            {
                trackedTasksDictionary.Add(group.Key, group.Value);
            }
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

        public async Task RefreshTable()
        {
            await LoadTrackedTasks(new PagerEventArgs { PageIndex = 1, Skip = 0, Top = 10 });
        }
    }
}
