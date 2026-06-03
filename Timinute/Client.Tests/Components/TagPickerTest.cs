using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radzen;
using Timinute.Client.Components;
using Timinute.Client.Helpers;
using Timinute.Client.Tests.Helpers;
using Timinute.Shared.Dtos.Tag;
using Xunit;

namespace Timinute.Client.Tests.Components
{
    public class TagPickerTest : BunitContext
    {
        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

        [Fact]
        public void RemoveButton_RemovesSelectedTagFromCallback()
        {
            RegisterServices(_ => Task.FromResult(OkJson(new List<TagDto>
            {
                new() { TagId = "tag-1", Name = "Work", Color = "#111111" },
                new() { TagId = "tag-2", Name = "Home", Color = "#222222" },
            })));

            List<string>? changed = null;
            var cut = Render<TagPicker>(parameters => parameters
                .Add(p => p.SelectedTagIds, new List<string> { "tag-1" })
                .Add(p => p.SelectedTagIdsChanged, EventCallback.Factory.Create<List<string>>(this, ids => changed = ids)));

            cut.WaitForElement(".tag-picker__remove").Click();
            cut.WaitForAssertion(() =>
            {
                Assert.NotNull(changed);
                Assert.Empty(changed!);
            });
        }

        [Fact]
        public void SelectingOption_AddsTagIdToCallback()
        {
            RegisterServices(_ => Task.FromResult(OkJson(new List<TagDto>
            {
                new() { TagId = "tag-1", Name = "Work", Color = "#111111" },
                new() { TagId = "tag-2", Name = "Home", Color = "#222222" },
            })));

            List<string>? changed = null;
            var cut = Render<TagPicker>(parameters => parameters
                .Add(p => p.SelectedTagIds, new List<string>())
                .Add(p => p.SelectedTagIdsChanged, EventCallback.Factory.Create<List<string>>(this, ids => changed = ids)));

            cut.Find(".tag-picker__add").Click();
            cut.WaitForAssertion(() =>
                Assert.Contains(
                    cut.FindAll(".tag-picker__option"),
                    button => button.TextContent.Contains("Work", StringComparison.OrdinalIgnoreCase)));
            cut.FindAll(".tag-picker__option")
                .Single(button => button.TextContent.Contains("Work", StringComparison.OrdinalIgnoreCase))
                .Click();

            Assert.NotNull(changed);
            Assert.Single(changed!);
            Assert.Equal("tag-1", changed[0]);
        }

        [Fact]
        public void CreateOption_PostsAndReturnsCreatedTagId()
        {
            string? postedColor = null;

            RegisterServices(async request =>
            {
                if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/Tag")
                {
                    return OkJson(new List<TagDto>
                    {
                        new() { TagId = "tag-1", Name = "Work", Color = "#111111" },
                    });
                }

                if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/Tag")
                {
                    var create = await request.Content!.ReadFromJsonAsync<CreateTagDto>();
                    postedColor = create?.Color;
                    return OkJson(new TagDto
                    {
                        TagId = "tag-new",
                        Name = "Focus",
                        Color = "#6366F1",
                    });
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            List<string>? changed = null;
            var cut = Render<TagPicker>(parameters => parameters
                .Add(p => p.SelectedTagIds, new List<string>())
                .Add(p => p.SelectedTagIdsChanged, EventCallback.Factory.Create<List<string>>(this, ids => changed = ids)));

            cut.Find(".tag-picker__add").Click();
            cut.WaitForElement(".tag-picker__search").Input("Focus");
            cut.WaitForElement(".tag-picker__create-color").Change("#00ff88");
            cut.WaitForElement(".tag-picker__option--create").Click();

            cut.WaitForAssertion(() =>
            {
                Assert.NotNull(changed);
                Assert.Single(changed!);
                Assert.Equal("tag-new", changed[0]);
                Assert.Equal("#00ff88", postedColor, ignoreCase: true);
            });
        }

        private void RegisterServices(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            Services.AddSingleton(new NotificationService());

            var handler = new StubHttpMessageHandler(responder);
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(Constants.API.ClientName)).Returns(client);
            Services.AddSingleton(factory.Object);
        }

        private static HttpResponseMessage OkJson<T>(T payload) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(payload, options: WebJson) };
    }
}
