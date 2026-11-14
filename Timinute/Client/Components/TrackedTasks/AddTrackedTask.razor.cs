using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;
using System.Globalization;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Client.Services;
using Timinute.Shared.Dtos.Project;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Components.TrackedTasks
{
    public partial class AddTrackedTask
    {
        [Parameter]
        public EventCallback<TrackedTask> OnTrackedTaskAdded { get; set; }

        // Keeps what the user typed, so an unparseable value is reported instead of silently
        // submitting the last valid duration.
        private string? durationText;
        private bool durationParseFailed;

        public string DurationProxy
        {
            get => durationText ?? NewTrackedTask.Duration.ToString();
            set
            {
                durationText = value;
                durationParseFailed = !TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var timeSpan);
                if (!durationParseFailed)
                {
                    NewTrackedTask.Duration = timeSpan;
                }
            }
        }

        private readonly List<Project> projects = new();
        
        private string SelectedProjectId { get; set; } = "";

        private readonly string idPrefix = $"entry-{Guid.NewGuid():N}";
        private string? durationError;
        private bool saving;

        private HttpClient client;

        private TrackedTask NewTrackedTask { get; set; } = new() { StartDate = DateTime.Now };

        #region Dependency Injection

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        [Inject]
        private NotificationService notificationService { get; set; } = null!;

        [Inject]
        private AuroraDialogService Dialogs { get; set; } = null!;

        [CascadingParameter]
        public DialogEntry? Entry { get; set; }

        #endregion

        protected override async Task OnInitializedAsync()
        {
            client = ClientFactory.CreateClient(Constants.API.ClientName);

            await LoadProjects();
        }

        private async Task HandleValidSubmit()
        {
            durationError = durationParseFailed
                ? "Enter the duration as hours:minutes:seconds, e.g. 1:30:00."
                : NewTrackedTask.Duration <= TimeSpan.Zero
                    ? "Enter a duration above zero, e.g. 1:30:00."
                    : null;
            if (durationError != null) return;

            saving = true;
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            CreateTrackedTaskDto createTrackedTaskDto = new()
            {
                Name = NewTrackedTask.Name,
                ProjectId = string.IsNullOrWhiteSpace(SelectedProjectId) ? null : SelectedProjectId,
                StartDate = NewTrackedTask.StartDate,
                Duration = NewTrackedTask.Duration,
                TagIds = NewTrackedTask.TagIds
            };

            try
            {
                var responseMessage = await client.PostAsJsonAsync(Constants.API.TrackedTask.Create, createTrackedTaskDto);
                responseMessage.EnsureSuccessStatusCode();

                await OnTrackedTaskAdded.InvokeAsync(NewTrackedTask);

                notificationService.Notify(NotificationSeverity.Success, "Success", "Tracked task saved", 3000);

                NewTrackedTask = new() { StartDate = DateTime.Now };
                durationText = null;

                Dialogs.Close(Entry, true);
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happened", ex.Message, 5000);
            }
            finally
            {
                saving = false;
            }
        }

        private Task OnTagIdsChanged(List<string> tagIds)
        {
            NewTrackedTask.TagIds = tagIds;
            return Task.CompletedTask;
        }

        private async Task LoadProjects()
        {
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
