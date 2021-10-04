using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models;
using Timinute.Shared.Dtos.TrackedTask;

namespace Timinute.Client.Pages.TrackedTasks
{
    public partial class TrackedTasks
    {
        public readonly IList<TrackedTask> TrackedTasksList = new List<TrackedTask>();

        private string exceptionMessage;

        [CascadingParameter]
        private Task<AuthenticationState> authenticationStateTask { get; set; }

        [Inject]
        protected NavigationManager Navigation { get; set; }

        [Inject]
        private IHttpClientFactory clientFactory { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var authState = await authenticationStateTask;
            var user = authState.User;

            if (user.Identity != null && !user.Identity.IsAuthenticated)
                Navigation.NavigateTo($"{Navigation.BaseUri}auth/login", true);

           // TrackedTasksList.Add(new TrackedTask { TaskId = "1", Name = "Task 1", Duration = TimeSpan.FromMinutes(120), StartDate = DateTime.Now, EndDate = DateTime.Now.AddMinutes(120) });
           // TrackedTasksList.Add(new TrackedTask { TaskId = "2", Name = "Task 2", Duration = TimeSpan.FromMinutes(60), StartDate = DateTime.Now, EndDate = DateTime.Now.AddMinutes(60) });
           // TrackedTasksList.Add(new TrackedTask { TaskId = "3", Name = "Task 3", Duration = TimeSpan.FromMinutes(180), StartDate = DateTime.Now, EndDate = DateTime.Now.AddMinutes(180) });

           //// ApiPath = $"{Navigation.BaseUri}api/ApplicationUserRead";
           
           await RefreshTable();
        }

        private async Task RefreshTable()
        {
            var client = clientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var response = await client.GetFromJsonAsync<TrackedTaskDto[]>(Constants.API.TrackedTask.GetAll);

                if (response != null)
                {
                    TrackedTasksList.Clear();

                    foreach (var item in response)
                        TrackedTasksList.Add(new TrackedTask(item));
                }
            }
            catch (Exception ex)
            {
                exceptionMessage = ex.Message;
            }
        }
    }
}
