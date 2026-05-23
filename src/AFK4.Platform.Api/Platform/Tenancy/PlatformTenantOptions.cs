namespace AFK4.Platform.Api.Platform.Tenancy;

public sealed class PlatformTenantOptions
{
    public const string ConfigurationSection = "PlatformTenant";

    public TimeSpan DefaultOwnerInviteLifetime { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan MaxOwnerInviteLifetime { get; set; } = TimeSpan.FromDays(30);
}
