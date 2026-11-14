using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Timinute.Client.Services;
using Timinute.Client.Shared;
using Xunit;

namespace Timinute.Client.Tests.Components
{
    public class AuroraDialogTest : BunitContext
    {
        private readonly AuroraDialogService dialogs = new();

        public AuroraDialogTest()
        {
            Services.AddSingleton(dialogs);
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <summary>Minimal dialog body: shows its parameter and closes with a fixed result.</summary>
        public class Probe : ComponentBase
        {
            [Parameter] public string? Text { get; set; }
            [Inject] private AuroraDialogService Dialogs { get; set; } = null!;

            protected override void BuildRenderTree(RenderTreeBuilder builder)
            {
                builder.OpenElement(0, "p");
                builder.AddAttribute(1, "class", "probe");
                builder.AddContent(2, Text);
                builder.CloseElement();
                builder.OpenElement(3, "button");
                builder.AddAttribute(4, "class", "probe-done");
                builder.AddAttribute(5, "onclick", EventCallback.Factory.Create(this, () => Dialogs.Close("done")));
                builder.CloseElement();
            }
        }

        [Fact]
        public void Open_Renders_Component_With_Parameters_In_Dialog_Role()
        {
            var host = Render<AuroraDialogHost>();

            _ = dialogs.OpenAsync<Probe>("Hello", new Dictionary<string, object?> { ["Text"] = "from caller" });

            host.WaitForAssertion(() =>
            {
                var panel = host.Find("[role=dialog]");
                Assert.Equal("true", panel.GetAttribute("aria-modal"));
                var titleId = panel.GetAttribute("aria-labelledby");
                Assert.Equal("Hello", host.Find($"#{titleId}").TextContent.Trim());
                Assert.Equal("from caller", host.Find(".probe").TextContent);
            });
        }

        [Fact]
        public async Task Close_Completes_Open_Task_With_Result()
        {
            var host = Render<AuroraDialogHost>();
            var open = dialogs.OpenAsync<Probe>("Hello");

            host.WaitForElement(".probe-done").Click();

            Assert.Equal("done", await open);
            host.WaitForAssertion(() => Assert.Empty(host.FindAll("[role=dialog]")));
        }

        [Fact]
        public async Task Escape_Closes_With_Null()
        {
            var host = Render<AuroraDialogHost>();
            var open = dialogs.OpenAsync<Probe>("Hello");

            host.WaitForElement("[role=dialog]").KeyDown("Escape");

            Assert.Null(await open);
            Assert.Empty(dialogs.Stack);
        }

        [Fact]
        public async Task Close_Button_And_Backdrop_Close_With_Null()
        {
            var host = Render<AuroraDialogHost>();
            var first = dialogs.OpenAsync<Probe>("One");
            host.WaitForElement(".aurora-modal__close").Click();
            Assert.Null(await first);

            var second = dialogs.OpenAsync<Probe>("Two");
            host.WaitForElement(".aurora-modal__backdrop").Click();
            Assert.Null(await second);
        }

        [Theory]
        [InlineData(".aurora-confirm__ok", true)]
        [InlineData(".aurora-confirm__cancel", false)]
        public async Task Confirm_Ok_True_Cancel_False(string button, bool expected)
        {
            var host = Render<AuroraDialogHost>();
            var confirm = dialogs.ConfirmAsync("Really?", "Check", okText: "Yes", cancelText: "No");

            host.WaitForAssertion(() => Assert.Contains("Really?", host.Find(".aurora-confirm__message").TextContent));
            Assert.Equal("Yes", host.Find(".aurora-confirm__ok").TextContent.Trim());
            Assert.Equal("No", host.Find(".aurora-confirm__cancel").TextContent.Trim());
            host.Find(button).Click();

            Assert.Equal(expected, await confirm);
        }

        [Fact]
        public void Danger_Confirm_Uses_Danger_Button_Class()
        {
            var host = Render<AuroraDialogHost>();

            _ = dialogs.ConfirmAsync("Delete it?", "Delete", okText: "Delete", danger: true);

            host.WaitForAssertion(() =>
            {
                Assert.Contains("aurora-btn--danger", host.Find(".aurora-confirm__ok").ClassName);
                // Destructive confirms start on the safe choice.
                Assert.True(host.Find(".aurora-confirm__cancel").HasAttribute("data-autofocus"));
            });
        }

        [Fact]
        public async Task Nested_Open_Closes_Topmost_First()
        {
            var host = Render<AuroraDialogHost>();
            var outer = dialogs.OpenAsync<Probe>("Outer", new Dictionary<string, object?> { ["Text"] = "outer" });
            var inner = dialogs.ConfirmAsync("Inner?", "Inner");

            host.WaitForAssertion(() => Assert.Equal(2, host.FindAll("[role=dialog]").Count));

            dialogs.Close();

            Assert.False(await inner);
            Assert.False(outer.IsCompleted);
            host.WaitForAssertion(() =>
            {
                Assert.Single(host.FindAll("[role=dialog]"));
                Assert.Equal("outer", host.Find(".probe").TextContent);
            });
            Assert.Equal("Outer", dialogs.Stack.Single().Title);
        }

        [Fact]
        public void Close_With_Nothing_Open_Is_A_NoOp()
        {
            dialogs.Close("ignored");

            Assert.Empty(dialogs.Stack);
        }
    }
}
