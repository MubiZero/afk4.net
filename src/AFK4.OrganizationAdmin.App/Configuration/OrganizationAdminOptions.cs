using AFK4.Localization;

namespace AFK4.OrganizationAdmin.App.Configuration;

public sealed class OrganizationAdminOptions
{
    public const string PlatformBaseUrlEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_PLATFORM_BASE_URL";
    public const string CurrencyCodeEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_CURRENCY_CODE";
    public const string OrganizationIdEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_ORGANIZATION_ID";
    public const string BranchIdEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_BRANCH_ID";
    public const string PreferredLocaleEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_PREFERRED_LOCALE";

    public Uri PlatformBaseUrl { get; init; } = new("http://localhost:5074");

    public string CurrencyCode { get; init; } = "TJS";

    public Guid? OrganizationId { get; init; }

    public Guid? BranchId { get; init; }

    /// <summary>
    /// The Organization Admin host's locale for the native WebView chrome (loading/failure
    /// overlay), provisioned into config and clamped to a supported locale. The React
    /// Organization Admin UI inside the WebView manages its own per-operator switcher. Default <c>ru</c>.
    /// </summary>
    public string PreferredLocale { get; init; } = Locales.Default;

    public static OrganizationAdminOptions LoadFromEnvironment()
    {
        return LoadFromEnvironment(Environment.GetEnvironmentVariable);
    }

    public static OrganizationAdminOptions LoadFromEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var options = new OrganizationAdminOptions();
        var platformBaseUrlValue = getEnvironmentVariable(PlatformBaseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(platformBaseUrlValue))
        {
            if (!Uri.TryCreate(platformBaseUrlValue.Trim(), UriKind.Absolute, out var platformBaseUrl) ||
                (platformBaseUrl.Scheme != Uri.UriSchemeHttp && platformBaseUrl.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"{PlatformBaseUrlEnvironmentVariable} must be an absolute http or https URL.");
            }

            options = new OrganizationAdminOptions
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

            options = new OrganizationAdminOptions
            {
                PlatformBaseUrl = options.PlatformBaseUrl,
                CurrencyCode = currencyCode,
                OrganizationId = options.OrganizationId,
                BranchId = options.BranchId
            };
        }

        var organizationIdValue = getEnvironmentVariable(OrganizationIdEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(organizationIdValue))
        {
            if (!Guid.TryParse(organizationIdValue.Trim(), out var organizationId))
            {
                throw new InvalidOperationException(
                    $"{OrganizationIdEnvironmentVariable} must be a GUID.");
            }

            options = new OrganizationAdminOptions
            {
                PlatformBaseUrl = options.PlatformBaseUrl,
                CurrencyCode = options.CurrencyCode,
                OrganizationId = organizationId,
                BranchId = options.BranchId
            };
        }

        var branchIdValue = getEnvironmentVariable(BranchIdEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(branchIdValue))
        {
            if (!Guid.TryParse(branchIdValue.Trim(), out var branchId))
            {
                throw new InvalidOperationException(
                    $"{BranchIdEnvironmentVariable} must be a GUID.");
            }

            options = new OrganizationAdminOptions
            {
                PlatformBaseUrl = options.PlatformBaseUrl,
                CurrencyCode = options.CurrencyCode,
                OrganizationId = options.OrganizationId,
                BranchId = branchId
            };
        }

        var preferredLocaleValue = getEnvironmentVariable(PreferredLocaleEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(preferredLocaleValue))
        {
            options = new OrganizationAdminOptions
            {
                PlatformBaseUrl = options.PlatformBaseUrl,
                CurrencyCode = options.CurrencyCode,
                OrganizationId = options.OrganizationId,
                BranchId = options.BranchId,
                PreferredLocale = Locales.Clamp(preferredLocaleValue.Trim())
            };
        }

        return options;
    }
}
