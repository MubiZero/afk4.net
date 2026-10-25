using Microsoft.AspNetCore.SignalR.Client;

namespace AFK4.Agent.Service;

/// <summary>
/// Повторы переподключения канала без предела: 1, 2, 4, 8, 16, 30 секунд и дальше по 30, плюс до
/// секунды случайно, чтобы зал после сбоя сети не стучался в хаб одним залпом. Встроенная политика
/// сдаётся после четырёх попыток (≈42 с), а интернет в клубе может пропасть на полчаса.
/// </summary>
public sealed class RealtimeRetryPolicy(Func<double>? random = null) : IRetryPolicy
{
    private readonly Func<double> random = random ?? Random.Shared.NextDouble;

    public TimeSpan? NextRetryDelay(RetryContext retryContext) => Delay(retryContext.PreviousRetryCount, random());

    public static TimeSpan Delay(long previousRetryCount, double jitter)
    {
        var seconds = Math.Min(30, Math.Pow(2, Math.Min(Math.Max(previousRetryCount, 0), 5)));
        return TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(Math.Floor(jitter * 1000));
    }
}
