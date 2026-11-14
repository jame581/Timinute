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

        private readonly string nameId = $"project-name-{Guid.NewGuid():N}";
        private readonly string colorLabelId = $"project-color-{Guid.NewGuid():N}";
        private bool saving;

        private IReadOnlyList<string> Palette => ColorService.GetPalette();

        #region Dependency Injection

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        [Inject]
        private NotificationService notificationService { get; set; } = null!;

        [Inject]
        private AuroraDialogService Dialogs { get; set; } = null!;

        [CascadingParameter]
        public DialogEntry? Entry { get; set; }

        [Inject]
        private ProjectColorService ColorService { get; set; } = null!;

        #endregion

        private static string ColorName(string hex) => hex.ToUpperInvariant() switch
        {
            "#6366F1" => "Indigo",
            "#F59E0B" => "Amber",
            "#10B981" => "Emerald",
            "#EC4899" => "Pink",
            "#94A3B8" => "Slate",
            _ => hex,
        };

        private void SelectColor(string color)
        {
            NewProject.Color = color;
        }

        private async Task HandleValidSubmit()
        {
            saving = true;
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

                Dialogs.Close(Entry, true);
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Could not create project", ex.Message, 5000);
            }
            finally
            {
                saving = false;
            }
        }
    }
}
