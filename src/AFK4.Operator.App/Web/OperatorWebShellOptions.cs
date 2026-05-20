namespace AFK4.Operator.App.Web;

public sealed class OperatorWebShellOptions
{
    public const string DevServerUrlEnvironmentVariable = "AFK4_OPERATOR_WEB_DEV_SERVER_URL";

    public Uri? DevServerUrl { get; init; }

    public static OperatorWebShellOptions LoadFromEnvironment()
    {
        return LoadFromEnvironment(Environment.GetEnvironmentVariable);
    }

    public static OperatorWebShellOptions LoadFromEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var devServerUrlValue = getEnvironmentVariable(DevServerUrlEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(devServerUrlValue))
        {
            return new OperatorWebShellOptions();
        }

        if (!Uri.TryCreate(devServerUrlValue.Trim(), UriKind.Absolute, out var devServerUrl) ||
            (devServerUrl.Scheme != Uri.UriSchemeHttp && devServerUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"{DevServerUrlEnvironmentVariable} must be an absolute http or https URL.");
        }

        return new OperatorWebShellOptions
        {
            DevServerUrl = devServerUrl
        };
    }
}
