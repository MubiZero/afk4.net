namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// The outcome of a single channel delivery attempt. A failure carries whether it is worth retrying
/// (transient SMTP/network error) or permanent (e.g. a bad address), which drives the dispatcher's
/// retry-vs-fail-fast decision (D11).
/// </summary>
public sealed record ChannelResult(bool Success, bool Retryable, string? Error)
{
    public static ChannelResult Sent() => new(Success: true, Retryable: false, Error: null);

    public static ChannelResult TransientFailure(string error) => new(Success: false, Retryable: true, Error: error);

    public static ChannelResult PermanentFailure(string error) => new(Success: false, Retryable: false, Error: error);
}
