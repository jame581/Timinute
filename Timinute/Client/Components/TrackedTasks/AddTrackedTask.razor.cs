﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Components.TrackedTasks
{
    public partial class AddTrackedTask
    {
        private TrackedTask NewTrackedTask { get; set; } = new() { StartDate = DateTime.Now };

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

        [Inject]
        private NotificationService notificationService { get; set; } = null!;

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
                notificationService.Notify(NotificationSeverity.Success, "Success", "Tracked task saved", 3000);

                NewTrackedTask = new() { StartDate = DateTime.Now };
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happend", ex.Message, 5000);
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
