using Radzen;

namespace Timinute.Client.Services
{
    public class UndoNotificationService
    {
        private readonly NotificationService notificationService;
        private readonly ILogger<UndoNotificationService> logger;

        public UndoNotificationService(NotificationService notificationService, ILogger<UndoNotificationService> logger)
        {
            this.notificationService = notificationService;
            this.logger = logger;
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
                        // Don't surface raw exception messages — they can leak internal
                        // details (URLs, server-side error strings). Log the actual
                        // exception for diagnostics, show a friendly summary to the user.
                        logger.LogError(ex, "Undo restore failed for {EntityLabel} '{EntityName}'", entityLabel, entityName);
                        notificationService.Notify(new NotificationMessage
                        {
                            Severity = NotificationSeverity.Error,
                            Summary = "Restore failed",
                            Detail = $"The {entityLabel.ToLowerInvariant()} could not be restored. Please try again.",
                            Duration = 5000
                        });
                    }
                }
            };

            notificationService.Notify(message);
        }
    }
}
