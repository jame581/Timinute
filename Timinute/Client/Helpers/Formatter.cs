namespace Timinute.Client.Helpers
{
    public static class Formatter
    {
        public static string FormatTimeSpan(double seconds)
        {
            TimeSpan totalTime = TimeSpan.FromSeconds(seconds);
            int hours = (totalTime.Days * 24) + totalTime.Hours;
            return $"{hours}:{totalTime.Minutes:00}:{totalTime.Seconds:00}";
        }

        public static string FormatTimeSpan(TimeSpan time)
        {
            int hours = (time.Days * 24) + time.Hours;
            return $"{hours}:{time.Minutes:00}:{time.Seconds:00}";
        }
    }
}
