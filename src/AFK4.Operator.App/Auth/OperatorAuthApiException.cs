namespace AFK4.Operator.App.Auth;

/// <summary>
/// A reset endpoint returned a structured business error (e.g. invalid_code with a
/// remaining-attempts count). Carries the backend error code and remaining attempts so the
/// host bridge can forward them to the web UI instead of collapsing to a generic message.
/// </summary>
public sealed class OperatorAuthApiException(string code, string message, int? remainingAttempts)
    : Exception(message)
{
    public string Code { get; } = code;
    public int? RemainingAttempts { get; } = remainingAttempts;
}
