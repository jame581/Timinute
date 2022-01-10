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
        private TrackedTask NewTrackedTask { get; set; } = new() { StartDate = DateTime.Now };

        private string exceptionMessage = "";

        bool displayValidationErrorMessages = false;

        public string DurationProxy
        {
            get => NewTrackedTask.Duration.ToString();
            set
            {
                if (TimeSpan.TryParse(value, out TimeSpan timeSpan))
                {
                    NewTrackedTask.Duration = timeSpan;
                }
            }
        }


        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        private async Task HandleValidSubmit()
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            CreateTrackedTaskDto createTrackedTaskDto = new()
            {
                Name = NewTrackedTask.Name,
                StartDate = NewTrackedTask.StartDate,
                Duration = NewTrackedTask.Duration
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
