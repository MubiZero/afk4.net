namespace AFK4.Platform.Api.Platform.Tenancy;

public sealed class PlatformOrganizationOptions
{
    public const string ConfigurationSection = "PlatformOrganization";

    public TimeSpan DefaultOrganizationOwnerInviteLifetime { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan MaxOrganizationOwnerInviteLifetime { get; set; } = TimeSpan.FromDays(30);
}
