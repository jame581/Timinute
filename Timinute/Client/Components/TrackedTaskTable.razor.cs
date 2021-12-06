using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Components
{
    public partial class TrackedTaskTable
    {
        public readonly Dictionary<string, List<TrackedTask>> TrackedTasksDictionary = new();

        private string exceptionMessage { get; set; }

        [Inject]
        private IHttpClientFactory clientFactory { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await LoadTrackedTasks();
        }

        private async Task LoadTrackedTasks()
        {
            exceptionMessage = "";
            var client = clientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var trackedTaskDtoList = await client.GetFromJsonAsync<TrackedTaskDto[]>(Constants.API.TrackedTask.GetAll);

                if (trackedTaskDtoList != null)
                {
                    TrackedTasksDictionary.Clear();

                    List<TrackedTask> trackedTaskList = new List<TrackedTask>();

                    foreach (var trackedTaskDto in trackedTaskDtoList)
                        trackedTaskList.Add(new TrackedTask(trackedTaskDto));

                    var groups = trackedTaskList.GroupBy(x => x.StartDate.ToLongDateString()).ToDictionary(x => x.Key, y => y.ToList());
                    foreach (var group in groups)
                    {
                        TrackedTasksDictionary.Add(group.Key, group.Value);
                    }

                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                exceptionMessage = ex.Message;
            }
        }
    }
}
