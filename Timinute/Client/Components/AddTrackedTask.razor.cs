using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Components
{
    public partial class AddTrackedTask
    {
        private TrackedTask trackedTask { get; set; } = new() { StartDate = DateTime.Now };

        private string exceptionMessage;

        bool displayValidationErrorMessages = false;

        public string DurationProxy
        {
            get => trackedTask.Duration.ToString();
            set
            {
                TimeSpan.TryParse(value, out TimeSpan timeSpan);
                trackedTask.Duration = timeSpan;
            }
        }


        [Inject]
        private IHttpClientFactory clientFactory { get; set; }

        private async Task HandleValidSubmit()
        {
            var client = clientFactory.CreateClient(Constants.API.ClientName);

            CreateTrackedTaskDto createTrackedTaskDto = new()
            {
                Name = trackedTask.Name,
                StartDate = trackedTask.StartDate,
                Duration = trackedTask.Duration
            };

            try
            {
                var responseMessage = await client.PostAsJsonAsync(Constants.API.TrackedTask.Create, createTrackedTaskDto);
                responseMessage.EnsureSuccessStatusCode();

                displayValidationErrorMessages = false;
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
