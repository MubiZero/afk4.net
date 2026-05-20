namespace AFK4.Operator.App.Configuration;

public sealed class OperatorAppOptions
{
    public const string PlatformBaseUrlEnvironmentVariable = "AFK4_OPERATOR_PLATFORM_BASE_URL";
    public const string CurrencyCodeEnvironmentVariable = "AFK4_OPERATOR_CURRENCY_CODE";

    public Uri PlatformBaseUrl { get; init; } = new("http://localhost:5074");

    public string CurrencyCode { get; init; } = "TJS";

    public Guid? OrganizationId { get; init; }

    public Guid? BranchId { get; init; }

    public static OperatorAppOptions LoadFromEnvironment()
    {
        return LoadFromEnvironment(Environment.GetEnvironmentVariable);
    }

    public static OperatorAppOptions LoadFromEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var options = new OperatorAppOptions();
        var platformBaseUrlValue = getEnvironmentVariable(PlatformBaseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(platformBaseUrlValue))
        {
            if (!Uri.TryCreate(platformBaseUrlValue.Trim(), UriKind.Absolute, out var platformBaseUrl) ||
                (platformBaseUrl.Scheme != Uri.UriSchemeHttp && platformBaseUrl.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"{PlatformBaseUrlEnvironmentVariable} must be an absolute http or https URL.");
            }

            options = new OperatorAppOptions
            {
                PlatformBaseUrl = platformBaseUrl,
                CurrencyCode = options.CurrencyCode,
                OrganizationId = options.OrganizationId,
                BranchId = options.BranchId
            };
        }

        var currencyCodeValue = getEnvironmentVariable(CurrencyCodeEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(currencyCodeValue))
        {
            var currencyCode = currencyCodeValue.Trim().ToUpperInvariant();
            if (currencyCode.Length != 3 || currencyCode.Any(character => character is < 'A' or > 'Z'))
            {
                throw new InvalidOperationException(
                    $"{CurrencyCodeEnvironmentVariable} must be a three-letter ISO currency code.");
            }

            options = new OperatorAppOptions
            {
                PlatformBaseUrl = options.PlatformBaseUrl,
                CurrencyCode = currencyCode,
                OrganizationId = options.OrganizationId,
                BranchId = options.BranchId
            };
        }

        return options;
    }
}
