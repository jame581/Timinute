using Microsoft.AspNetCore.Components;
using Radzen;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Components.TrackedTasks
{
    public partial class TrackedTaskTable
    {
        public readonly Dictionary<string, List<TrackedTask>> trackedTasksDictionary = new();

        #region Dependency Injection

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        [Inject]
        private NotificationService notificationService { get; set; } = null!;

        #endregion

        protected override async Task OnInitializedAsync()
        {
            await LoadTrackedTasks();
        }

        private async Task LoadTrackedTasks()
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var trackedTaskDtoList = await client.GetFromJsonAsync<TrackedTaskDto[]>(Constants.API.TrackedTask.GetAll);

                if (trackedTaskDtoList != null)
                {
                    trackedTasksDictionary.Clear();

                    List<TrackedTask> trackedTaskList = new();

                    foreach (var trackedTaskDto in trackedTaskDtoList)
                        trackedTaskList.Add(new TrackedTask(trackedTaskDto));

                    GroupTraskedTasks(trackedTaskList);

                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happend", ex.Message, 5000);
            }
        }

        private void GroupTraskedTasks(List<TrackedTask> trackedTaskList)
        {
            var groups = trackedTaskList.GroupBy(x => x.StartDate.ToLongDateString()).ToDictionary(x => x.Key, y => y.ToList());
            foreach (var group in groups)
            {
                trackedTasksDictionary.Add(group.Key, group.Value);
            }
        }

        public async Task RefreshTable()
        {
            await LoadTrackedTasks();
        }
    }
}
