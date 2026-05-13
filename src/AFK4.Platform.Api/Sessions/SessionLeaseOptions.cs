namespace AFK4.Platform.Api.Sessions;

public sealed class SessionLeaseOptions
{
    public string SigningPrivateKeyPem { get; set; } = string.Empty;

    public int LeaseMinutes { get; set; } = 15;
}
