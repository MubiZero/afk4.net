namespace AFK4.SetupWizard.Web;

public sealed class SetupWizardWebShellOptions
{
    public const string DevServerUrlEnvironmentVariable = "AFK4_SETUP_WIZARD_WEB_DEV_SERVER_URL";

    public Uri? DevServerUrl { get; init; }

    public static SetupWizardWebShellOptions LoadFromEnvironment()
    {
        return LoadFromEnvironment(Environment.GetEnvironmentVariable);
    }

    public static SetupWizardWebShellOptions LoadFromEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var devServerUrlValue = getEnvironmentVariable(DevServerUrlEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(devServerUrlValue))
        {
            return new SetupWizardWebShellOptions();
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

        return new SetupWizardWebShellOptions
        {
            DevServerUrl = devServerUrl
        };
    }
}
