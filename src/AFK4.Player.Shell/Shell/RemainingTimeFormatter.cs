namespace AFK4.Player.Shell.Shell;

public static class RemainingTimeFormatter
{
    public static string Format(int? remainingSeconds)
    {
        if (remainingSeconds is null)
        {
            return "--:--";
        }

        var clamped = Math.Max(0, remainingSeconds.Value);
        var time = TimeSpan.FromSeconds(clamped);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }
}
