namespace AFK4.Platform.Api.Platform.Tenancy;

public sealed class PlatformTenantOptions
{
    public const string ConfigurationSection = "PlatformTenant";

    public TimeSpan DefaultOrganizationOwnerInviteLifetime { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan MaxOrganizationOwnerInviteLifetime { get; set; } = TimeSpan.FromDays(30);
}
