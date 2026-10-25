using AFK4.Agent.Service;
using Microsoft.AspNetCore.SignalR.Client;

namespace AFK4.Agent.Service.Tests;

public sealed class RealtimeRetryPolicyTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 8)]
    [InlineData(5, 30)]
    [InlineData(1_000, 30)]
    public void Delay_GrowsToThirtySecondsAndStaysThere(long previousRetries, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), RealtimeRetryPolicy.Delay(previousRetries, jitter: 0));
    }

    [Fact]
    public void Policy_NeverGivesUp_AndAddsUpToASecondOfJitter()
    {
        var policy = new RealtimeRetryPolicy(() => 0.999);

        var delay = policy.NextRetryDelay(new RetryContext { PreviousRetryCount = 10_000, ElapsedTime = TimeSpan.FromHours(3) });

        Assert.Equal(TimeSpan.FromSeconds(30) + TimeSpan.FromMilliseconds(999), delay);
    }
}
