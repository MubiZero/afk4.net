namespace AFK4.Operator.App.Configuration;

public sealed class OperatorAppOptions
{
    public const string PlatformBaseUrlEnvironmentVariable = "AFK4_OPERATOR_PLATFORM_BASE_URL";

    public Uri PlatformBaseUrl { get; init; } = new("http://localhost:5074");

    public Guid? OrganizationId { get; init; }

    public Guid? BranchId { get; init; }

    public static OperatorAppOptions LoadFromEnvironment()
    {
        return LoadFromEnvironment(Environment.GetEnvironmentVariable);
    }

    public static OperatorAppOptions LoadFromEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var platformBaseUrlValue = getEnvironmentVariable(PlatformBaseUrlEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(platformBaseUrlValue))
        {
            return new OperatorAppOptions();
        }

        if (!Uri.TryCreate(platformBaseUrlValue.Trim(), UriKind.Absolute, out var platformBaseUrl) ||
            (platformBaseUrl.Scheme != Uri.UriSchemeHttp && platformBaseUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"{PlatformBaseUrlEnvironmentVariable} must be an absolute http or https URL.");
        }

        return new OperatorAppOptions
        {
            PlatformBaseUrl = platformBaseUrl
        };
    }
}
