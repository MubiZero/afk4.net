using AFK4.Player.Shell.Shell;

namespace AFK4.Player.Shell.Tests;

public sealed class RemainingTimeFormatterTests
{
    [Theory]
    [InlineData(null, "--:--")]
    [InlineData(0, "00:00")]
    [InlineData(59, "00:59")]
    [InlineData(60, "01:00")]
    [InlineData(3661, "01:01:01")]
    public void Format_ReturnsOperatorReadableCountdown(int? seconds, string expected)
    {
        Assert.Equal(expected, RemainingTimeFormatter.Format(seconds));
    }
}
