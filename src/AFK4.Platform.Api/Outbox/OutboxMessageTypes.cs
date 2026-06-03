namespace AFK4.Platform.Api.Outbox;

/// <summary>The closed set of <see cref="Data.OutboxMessageEntity.Type"/> discriminators.</summary>
public static class OutboxMessageTypes
{
    /// <summary>Post-commit effect of a session checkout: wake the device for its lock command.</summary>
    public const string SessionCheckout = "session.checkout";
}
