using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using Radzen.Blazor;
using System.Net.Http.Json;
using Timinute.Client.Components.Scheduler;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Pages.TrackedTasks
{
    public partial class TrackedTaskScheduler
    {
        private readonly IList<TrackedTask> trackedTasksList = new List<TrackedTask>();

        private bool isLoading = true;

        private RadzenScheduler<TrackedTask> radzenScheduler = null!;

        [CascadingParameter]
        private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;

        #region Dependency Injection

        [Inject]
        protected NavigationManager Navigation { get; set; } = null!;

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        [Inject]
        private NotificationService notificationService { get; set; } = null!;

        [Inject]
        private DialogService dialogService { get; set; } = null!;

        #endregion

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthenticationStateTask;
            var user = authState.User;

            if (user.Identity != null && !user.Identity.IsAuthenticated)
                Navigation.NavigateTo($"{Navigation.BaseUri}auth/login", true);

            await LoadData();
            await radzenScheduler.Reload();
        }

        private async Task LoadData()
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);
            isLoading = true;
            try
            {
                var response = await client.GetFromJsonAsync<TrackedTaskDto[]>(Constants.API.TrackedTask.GetAll);

                if (response != null)
                {
                    trackedTasksList.Clear();

                    foreach (var item in response)
                        trackedTasksList.Add(new TrackedTask(item));

                    //tasksCount = trackedTasksList.Count;
                }
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happened", ex.Message, 5000);
            }

            isLoading = false;
        }

        void OnSlotRender(SchedulerSlotRenderEventArgs args)
        {
            // Highlight today in month view
            if (args.View.Text == "Month" && args.Start.Date == DateTime.Today)
            {
                args.Attributes["style"] = "background: rgba(255,220,40,.2);";
            }

            // Highlight working hours (9-18)
            if ((args.View.Text == "Week" || args.View.Text == "Day") && args.Start.Hour > 8 && args.Start.Hour < 19)
            {
                args.Attributes["style"] = "background: rgba(255,220,40,.2);";
            }
        }

        async Task OnSlotSelect(SchedulerSlotSelectEventArgs args)
        {
            //console.Log($"SlotSelect: Start={args.Start} End={args.End}");

            TrackedTask data = await dialogService.OpenAsync<AddTrackedTaskForm>("Add Tracked Task",
                new Dictionary<string, object> { { "Start", args.Start }, { "End", args.End } });

            if (data != null)
            {
                //appointments.Add(data);
                // Either call the Reload method or reassign the Data property of the Scheduler
                await radzenScheduler.Reload();
            }
        }

        async Task OnTrackedTaskSelect(SchedulerAppointmentSelectEventArgs<TrackedTask> args)
        {
            //console.Log($"AppointmentSelect: Appointment={args.Data.Text}");

            await dialogService.OpenAsync<EditTrackedTaskForm>("Edit Tracked Task", new Dictionary<string, object> { { "Tracked Task", args.Data } });

            await radzenScheduler.Reload();
        }

        void OnTrackedTaskRender(SchedulerAppointmentRenderEventArgs<TrackedTask> args)
        {
            //if (args.Data.Name == "Birthday")
            //{
            //    args.Attributes["style"] = "background: red";
            //}
        }
    }
}
