using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen.Blazor;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models.Dashboard;
using Timinute.Shared.Dtos.Dashboard;

namespace Timinute.Client.Components.Dashboard
{
    public partial class ProjectColumnChart
    {
        private IList<WorkTimePerMonth> workTimePerMonths = new List<WorkTimePerMonth>();

        private RadzenChart radzenChartComponet = null!;

        [CascadingParameter]
        private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;

        [Inject]
        protected NavigationManager Navigation { get; set; } = null!;

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthenticationStateTask;
            var user = authState.User;

            if (user.Identity != null && !user.Identity.IsAuthenticated)
                Navigation.NavigateTo($"{Navigation.BaseUri}auth/login", true);

            await LoadDataForChart();
        }

        private async Task LoadDataForChart()
        {
            var client = ClientFactory.CreateClient(Constants.API.ClientName);

            try
            {
                var response = await client.GetFromJsonAsync<WorkTimePerMonthDto[]>(Constants.API.Analytics.GetWorkTimePerMonths);

                if (response != null)
                {
                    workTimePerMonths.Clear();

                    foreach (var item in response)
                    {
                        var workTimePerMonth = new WorkTimePerMonth
                        {
                            Time = item.Time,
                            WorkTimeInSeconds = item.WorkTimeInSeconds,
                        };
                       
                        workTimePerMonths.Add(workTimePerMonth);
                    }
                }

                await radzenChartComponet.Reload();
            }
            catch (Exception ex)
            {
                // TODO(jame_581): Add notification
            }
        }       

        string FormatTimeAsString(object value)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds((double)value);
            return timeSpan.ToString(@"hh\:mm\:ss");
        }
    }
}
