using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.Project;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Components
{
    public partial class TimeTracker
    {
        private TrackedTask trackedTask = new();

        private List<Project> projects = new();

        public string? projectId { get; set; }

        private bool stopWatchRunning = false;

        private string durationProxy { get; set; } = "00:00:00";

        private string? exceptionMessage { get; set; }

        bool displayValidationErrorMessages = false;

        [Parameter]
        public EventCallback<TrackedTask> OnAddTrackedTask { get; set; }

        [Inject]
        private IHttpClientFactory clientFactory { get; set; }

        [Inject]
        private ISessionStorageService sessionStorage { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await LoadProjects();

            var savedTrackedTask = await sessionStorage.GetItemAsync<TrackedTask>("trackedTask");
            
            if (savedTrackedTask != null)
            {
                trackedTask = savedTrackedTask;
                projectId = trackedTask.ProjectId;
                stopWatchRunning = true;
                await StopWatchTick();
            }
        }

        private async Task HandleValidSubmit()
        {
            displayValidationErrorMessages = false;

            if (stopWatchRunning)
            {
                await StopStopWatch();
            }
            else
            {
                await StartWatch();
            }
        }

        private void HandleInvalidSubmit()
        {
            displayValidationErrorMessages = true;
        }

        private async Task StartWatch()
        {
            trackedTask.StartDate = DateTime.Now;
            trackedTask.Duration = new TimeSpan();

            var client = clientFactory.CreateClient(Constants.API.ClientName);

            CreateTrackedTaskDto createTrackedTaskDto = new()
            {
                Name = trackedTask.Name,
                StartDate = trackedTask.StartDate
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

                    await sessionStorage.SetItemAsync<TrackedTask>("trackedTask", trackedTask);
                }

            }
            catch (Exception ex)
            {
                exceptionMessage = ex.Message;
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
                    durationProxy = trackedTask.Duration.ToString();
                    StateHasChanged();
                }
            }
        }

        private async Task StopStopWatch()
        {
            var client = clientFactory.CreateClient(Constants.API.ClientName);

            UpdateTrackedTaskDto updateTrackedTaskDto = new()
            {
                TaskId = trackedTask.TaskId,
                Name = trackedTask.Name,
                ProjectId = projectId,
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
                durationProxy = "00:00:00";

                StateHasChanged();
            }
            catch (Exception ex)
            {
                exceptionMessage = ex.Message;
            }
        }

        private async Task LoadProjects()
        {
            var client = clientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var responseMessage = await client.GetFromJsonAsync<List<ProjectDto>>(Constants.API.Project.GetAll);
                
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
                exceptionMessage = ex.Message;
            }
        }
    }
}
