using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.Project;

namespace Timinute.Client.Components
{
    public partial class AddProject
    {
        private Project NewProject { get; set; } = new();

        private string exceptionMessage = "";

        bool displayValidationErrorMessages = false;

        [Parameter]
        public EventCallback<Project> OnAddProject { get; set; }

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        private async Task HandleValidSubmit()
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            ProjectDto createProjectDto = new()
            {
                Name = NewProject.Name
            };

            try
            {
                var responseMessage = await client.PostAsJsonAsync(Constants.API.Project.Create, createProjectDto);
                responseMessage.EnsureSuccessStatusCode();

                displayValidationErrorMessages = false;

                await OnAddProject.InvokeAsync(NewProject);
            }
            catch (Exception ex)
            {
                exceptionMessage = ex.Message;
                displayValidationErrorMessages = true;
            }
        }

        private void HandleInvalidSubmit(EditContext context)
        {
            displayValidationErrorMessages = true;
        }
    }
}
