using Timinute.Client.Models;

namespace Timinute.Client.Components
{
    public partial class AddTrackedTask
    {
        private TrackedTask trackedTask { get; set; }


        public string DurationProxy
        {
            get => trackedTask.Duration.ToString();
            set
            {
                TimeSpan.TryParse(value, out TimeSpan timeSpan);
                trackedTask.Duration = timeSpan;
            }
        }

        private void HandleValidSubmit()
        {
            //Logger.LogInformation("HandleValidSubmit called");

            // Process the valid form

            //trackedTask.Duration = trackedTask.DurationProxy;
        }

        protected override void OnInitialized()
        {
            trackedTask = new TrackedTask() { StartDate = DateTime.Now };
        }
    }
}
