using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.Project;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Components.TrackedTasks
{
    public partial class TrackTaskTime
    {
        public string? ProjectId { get; set; }

        private TrackedTask trackedTask = new();

        private readonly List<Project> projects = new();

        private bool stopWatchRunning = false;

        private string DurationProxy { get; set; } = "00:00:00";

        [Parameter]
        public EventCallback<TrackedTask> OnAddTrackedTask { get; set; }

        #region Dependency Injection

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        [Inject]
        private ISessionStorageService SessionStorage { get; set; } = null!;

        [Inject]
        private NotificationService notificationService { get; set; } = null!;

        #endregion

        protected override async Task OnInitializedAsync()
        {
            await LoadProjects();

            var savedTrackedTask = await SessionStorage.GetItemAsync<TrackedTask>("trackedTask");

            if (savedTrackedTask != null)
            {
                trackedTask = savedTrackedTask;
                trackedTask.Duration = DateTime.Now - trackedTask.StartDate;
                ProjectId = trackedTask.ProjectId;
                stopWatchRunning = true;
                await StopWatchTick();
            }
        }

        private async Task HandleValidSubmit()
        {
            if (stopWatchRunning)
            {
                await StopStopWatch();
            }
            else
            {
                await StartWatch();
            }
        }

        private void HandleInvalidSubmit(EditContext context)
        {
            var errorMessages = context.GetValidationMessages();
            foreach (var errorMessage in errorMessages)
            {
                notificationService.Notify(NotificationSeverity.Error, "Validation error", errorMessage, 5000);
            }
        }

        private async Task StartWatch()
        {
            trackedTask.StartDate = DateTime.Now;
            trackedTask.Duration = new TimeSpan();
            trackedTask.ProjectId = ProjectId;

            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            CreateTrackedTaskDto createTrackedTaskDto = new()
            {
                Name = trackedTask.Name,
                StartDate = trackedTask.StartDate,
                ProjectId = trackedTask.ProjectId
            };

            try
            {
                var responseMessage = await client.PostAsJsonAsync(Constants.API.TrackedTask.Create, createTrackedTaskDto);
                responseMessage.EnsureSuccessStatusCode();

                var trackedTaskDto = await responseMessage.Content.ReadFromJsonAsync<TrackedTaskDto>();

                if (trackedTaskDto != null)
                {
                    trackedTask.TaskId = trackedTaskDto.TaskId;
                    stopWatchRunning = true;

                    await SessionStorage.SetItemAsync<TrackedTask>("trackedTask", trackedTask);
                    notificationService.Notify(NotificationSeverity.Success, "Success", "Tracking time started", 3000);
                }

            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happened", ex.Message, 5000);
            }

            await StopWatchTick();
        }

        private async Task StopWatchTick()
        {
            while (stopWatchRunning)
            {
                await Task.Delay(1000);

                if (stopWatchRunning)
                {
                    trackedTask.Duration = trackedTask.Duration.Add(TimeSpan.FromSeconds(1));
                    DurationProxy = trackedTask.Duration.ToString(@"hh\:mm\:ss");
                    StateHasChanged();
                }
            }
        }

        private async Task StopStopWatch()
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            UpdateTrackedTaskDto updateTrackedTaskDto = new()
            {
                TaskId = trackedTask.TaskId,
                Name = trackedTask.Name,
                ProjectId = ProjectId,
                StartDate = trackedTask.StartDate,
                EndDate = trackedTask.StartDate + trackedTask.Duration
            };

            try
            {
                var responseMessage = await client.PutAsJsonAsync(Constants.API.TrackedTask.Update, updateTrackedTaskDto);
                responseMessage.EnsureSuccessStatusCode();

                await OnAddTrackedTask.InvokeAsync(trackedTask);

                trackedTask = new();
                stopWatchRunning = false;
                DurationProxy = "00:00:00";
                ProjectId = null;

                await SessionStorage.RemoveItemAsync("trackedTask");
                notificationService.Notify(NotificationSeverity.Success, "Success", "Tracking time stopped", 3000);
                StateHasChanged();
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happened", ex.Message, 5000);
            }
        }

        private async Task LoadProjects()
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var responseMessage = await client.GetFromJsonAsync<List<ProjectDto>>(Constants.API.Project.Get);

                if (responseMessage != null)
                {
                    projects.Clear();
                    foreach (var projectDto in responseMessage)
                    {
                        projects.Add(new Project(projectDto));
                    }
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happened", ex.Message, 5000);
            }
        }
    }
}
