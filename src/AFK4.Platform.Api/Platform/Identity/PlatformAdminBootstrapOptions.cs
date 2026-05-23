namespace AFK4.Platform.Api.Platform.Identity;

public sealed class PlatformAdminBootstrapOptions
{
    public const string ConfigurationSection = "PlatformAdmin:Bootstrap";

    public string? UserName { get; set; }

    public string? DisplayName { get; set; }

    public string? Password { get; set; }

    public string[]? Roles { get; set; }
}
