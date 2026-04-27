namespace Timinute.Client.Services
{
    /// <summary>
    /// Tiny scoped service that lets the mobile topbar avatar tell the
    /// MobileMoreSheet to show itself without a direct component reference.
    /// </summary>
    public class MobileSheetService
    {
        public bool IsOpen { get; private set; }
        public event Action? OnChange;

        public void Show()
        {
            if (IsOpen) return;
            IsOpen = true;
            OnChange?.Invoke();
        }

        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            OnChange?.Invoke();
        }

        public void Toggle()
        {
            if (IsOpen) Hide(); else Show();
        }
    }
}
