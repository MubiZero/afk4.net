namespace AFK4.Platform.Api.Configuration;

public sealed class OrganizationAdminCompatibilityOptions
{
    public const string SectionName = "OrganizationAdminCompatibility";

    public int RequiredEpoch { get; init; } = 2;

    public string DownloadUrl { get; init; } = string.Empty;
}
