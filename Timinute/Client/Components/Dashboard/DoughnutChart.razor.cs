﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using Radzen.Blazor;
using System.Net.Http.Json;
using Timinute.Client.Helpers;
using Timinute.Client.Models.Dashboard;
using Timinute.Shared.Dtos.Dashboard;

namespace Timinute.Client.Components.Dashboard
{
    public partial class DoughnutChart
    {
        private IList<ProjectDataItem> projectTime = new List<ProjectDataItem>();

        private RadzenChart radzenChartComponet = null!;

        [CascadingParameter]
        private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;

        #region Dependency Injection

        [Inject]
        protected NavigationManager Navigation { get; set; } = null!;

        [Inject]
        private IHttpClientFactory ClientFactory { get; set; } = null!;

        [Inject]
        private NotificationService notificationService { get; set; } = null!;

        #endregion

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
                var response = await client.GetFromJsonAsync<ProjectDataItemDto[]>(Constants.API.Analytics.GetProjectWorkTime);

                if (response != null)
                {
                    projectTime.Clear();

                    foreach (var item in response)
                        projectTime.Add(new ProjectDataItem(item.ProjectId, item.ProjectName, item.Time, ""));
                }

                await radzenChartComponet.Reload();
            }
            catch (Exception ex)
            {
                notificationService.Notify(NotificationSeverity.Error, "Something happened", ex.Message, 5000);
            }
        }

        string FormatTimeAsString(object value)
        {
            return Formatter.FormatTimeSpan((double)value);
        }
    }
}
