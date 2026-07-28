namespace AFK4.OrganizationAdmin.Web;

public sealed class OrganizationAdminWebShellOptions
{
    public const string DevServerUrlEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_WEB_DEV_SERVER_URL";

    public Uri? DevServerUrl { get; init; }

    public static OrganizationAdminWebShellOptions LoadFromEnvironment()
    {
        return LoadFromEnvironment(Environment.GetEnvironmentVariable);
    }

    public static OrganizationAdminWebShellOptions LoadFromEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var devServerUrlValue = getEnvironmentVariable(DevServerUrlEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(devServerUrlValue))
        {
            return new OrganizationAdminWebShellOptions();
        }

        if (!Uri.TryCreate(devServerUrlValue.Trim(), UriKind.Absolute, out var devServerUrl) ||
            (devServerUrl.Scheme != Uri.UriSchemeHttp && devServerUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"{DevServerUrlEnvironmentVariable} must be an absolute http or https URL.");
        }

        if (!devServerUrl.IsLoopback)
        {
            throw new InvalidOperationException(
                $"{DevServerUrlEnvironmentVariable} must point to localhost or a loopback address.");
        }

        return new OrganizationAdminWebShellOptions
        {
            DevServerUrl = devServerUrl
        };
    }
}
