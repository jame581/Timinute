using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Radzen;
using Timinute.Client.Components.Scheduler;
using Timinute.Client.Models;
using Timinute.Client.Services;
using Timinute.Client.Tests.Helpers;
using Timinute.Shared.Dtos.Project;
using Timinute.Shared.Dtos.Tag;
using Xunit;

namespace Timinute.Client.Tests.Components
{
    public class TaskDialogFormsTest : BunitContext
    {
        private readonly AuroraDialogService dialogs = new();

        public TaskDialogFormsTest()
        {
            Services.AddSingleton(dialogs);
            Services.AddScoped<NotificationService>();
            Services.AddScoped(sp => new UndoNotificationService(sp.GetRequiredService<NotificationService>(), Mock.Of<ILogger<UndoNotificationService>>()));
            JSInterop.Mode = JSRuntimeMode.Loose;

            // Projects load slowly, so the form renders before OnInitializedAsync finishes.
            var handler = new StubHttpMessageHandler(async req =>
            {
                await Task.Delay(50);
                return req.RequestUri!.AbsolutePath.Contains("Tag")
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<TagDto>()) }
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<ProjectDto> { new() { ProjectId = "p1", Name = "Website", Color = "#10B981" } }) };
            });
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });
            Services.AddSingleton(factory.Object);
        }

        [Fact]
        public void Edit_Form_Shows_The_Task_On_First_Render()
        {
            var task = new TrackedTask { TaskId = "t1", Name = "Release notes", StartDate = new DateTime(2026, 11, 14, 9, 0, 0), EndDate = new DateTime(2026, 11, 14, 10, 30, 0) };

            var cut = Render<EditTrackedTaskForm>(ps => ps.Add(p => p.TrackedTask, task));

            Assert.Equal("Release notes", cut.Find("input[name='Model.Name']").GetAttribute("value"));
            Assert.Equal("2026-11-14T09:00:00", cut.Find("input[name='Model.StartDate']").GetAttribute("value"));
            Assert.Equal("2026-11-14T10:30:00", cut.Find("input[name='Model.EndDate']").GetAttribute("value"));
        }

        [Fact]
        public void Add_Form_Starts_At_The_Clicked_Slot()
        {
            var slot = new DateTime(2026, 11, 14, 13, 0, 0);

            var cut = Render<AddTrackedTaskForm>(ps => ps.Add(p => p.Start, slot).Add(p => p.End, slot.AddHours(1)));

            Assert.Equal("2026-11-14T13:00:00", cut.Find("input[name='Model.StartDate']").GetAttribute("value"));
            Assert.Equal("2026-11-14T14:00:00", cut.Find("input[name='Model.EndDate']").GetAttribute("value"));
        }

        [Fact]
        public async Task Edit_Form_Rejects_End_Before_Start_Without_Closing()
        {
            var open = dialogs.OpenAsync<EditTrackedTaskForm>("Edit", new Dictionary<string, object?>
            {
                ["TrackedTask"] = new TrackedTask { TaskId = "t1", Name = "Release notes", StartDate = new DateTime(2026, 11, 14, 9, 0, 0), EndDate = new DateTime(2026, 11, 14, 8, 0, 0) },
            });
            var host = Render<Timinute.Client.Shared.AuroraDialogHost>();

            host.WaitForElement("form").Submit();

            host.WaitForAssertion(() => Assert.Contains("End must be after start.", host.Find(".aurora-form__error").TextContent));
            Assert.False(open.IsCompleted);
            await Task.CompletedTask;
        }

        [Fact]
        public void Quick_Add_Reports_Unparseable_Duration_Instead_Of_Saving()
        {
            var cut = Render<Timinute.Client.Components.TrackedTasks.AddTrackedTask>();

            cut.Find("input[placeholder='What did you work on?']").Change("Release notes");
            var duration = cut.Find("input[placeholder='1:30:00']");
            duration.Change("1:30:00");
            duration.Change("ninety minutes");
            cut.Find("form").Submit();

            cut.WaitForAssertion(() => Assert.Contains("hours:minutes:seconds", cut.Find(".aurora-form__error").TextContent));
            Assert.Equal("ninety minutes", cut.Find("input[placeholder='1:30:00']").GetAttribute("value"));
        }
    }
}
