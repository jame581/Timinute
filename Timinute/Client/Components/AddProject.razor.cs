using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Client.Services;
using Timinute.Shared.Dtos.Project;

namespace Timinute.Client.Components
{
    public partial class AddProject
    {
        [Parameter]
        public EventCallback<Project> OnAddProject { get; set; }

        private Project NewProject { get; set; } = new();

        private IReadOnlyList<string> Palette => ColorService.GetPalette();

        #region Dependency Injection

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        [Inject]
        private NotificationService notificationService { get; set; } = null!;

        [Inject]
        private DialogService DialogService { get; set; } = null!;

        [Inject]
        private ProjectColorService ColorService { get; set; } = null!;

        #endregion

        private void SelectColor(string color)
        {
            NewProject.Color = color;
        }

        private async Task HandleValidSubmit()
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            var createProjectDto = new CreateProjectDto
            {
                Name = NewProject.Name,
                Color = NewProject.Color
            };

            try
            {
                var responseMessage = await client.PostAsJsonAsync(Constants.API.Project.Create, createProjectDto);
                responseMessage.EnsureSuccessStatusCode();

                var created = await responseMessage.Content.ReadFromJsonAsync<ProjectDto>();
                if (created != null)
                {
                    NewProject.ProjectId = created.ProjectId;
                    NewProject.Color = created.Color;
                }

                await OnAddProject.InvokeAsync(NewProject);

                NewProject = new Project();

                DialogService.Close(true);
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Validation error", ex.Message, 5000);
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
    }
}
