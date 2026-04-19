using Radzen;

namespace Timinute.Client.Services
{
    public class UndoNotificationService
    {
        private readonly NotificationService notificationService;

        public UndoNotificationService(NotificationService notificationService)
        {
            this.notificationService = notificationService;
        }

        public void ShowUndo(string entityLabel, string entityName, Func<Task> onUndo)
        {
            var message = new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = $"{entityLabel} '{entityName}' deleted",
                Detail = "Click to undo.",
                Duration = 8000,
                Click = async _ =>
                {
                    try
                    {
                        await onUndo();
                        notificationService.Notify(new NotificationMessage
                        {
                            Severity = NotificationSeverity.Success,
                            Summary = $"{entityLabel} restored",
                            Duration = 4000
                        });
                    }
                    catch (Exception ex)
                    {
                        notificationService.Notify(new NotificationMessage
                        {
                            Severity = NotificationSeverity.Error,
                            Summary = "Restore failed",
                            Detail = ex.Message,
                            Duration = 5000
                        });
                    }
                }
            };

            notificationService.Notify(message);
        }
    }
}
