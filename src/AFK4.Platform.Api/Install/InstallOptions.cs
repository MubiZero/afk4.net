using AFK4.Shared.Contracts.Updates;

namespace AFK4.Platform.Api.Install;

public sealed class InstallOptions
{
    public const string SectionName = "Install";

    public string ApiBaseUrl { get; set; } = "http://localhost:5074";

    public string UpdateChannel { get; set; } = UpdateChannelNames.Internal;

    public string LeaseSigningPublicKeyPem { get; set; } = string.Empty;

    public string UpdatePackageSigningPublicKeyPem { get; set; } = string.Empty;
}
