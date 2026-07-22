using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Payments.Eskhata;

public sealed class EskhataMerchantClientFactory(
    IHttpClientFactory httpClientFactory,
    PlatformDbContext db,
    ISecretProtector secretProtector) : IEskhataMerchantClientFactory
{
    public const string HttpClientName = "eskhata";

    public async Task<IEskhataMerchantClient?> CreateForOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken)
    {
        var config = await db.EskhataMerchantConfigs.AsNoTracking()
            .SingleOrDefaultAsync(c => c.OrganizationId == organizationId && c.BranchId == null, cancellationToken);
        if (config is null || string.IsNullOrEmpty(config.HashKeyEncrypted)
            || string.IsNullOrWhiteSpace(config.BaseUrl) || string.IsNullOrWhiteSpace(config.CompanyId))
        {
            return null;
        }

        string hashKey;
        try { hashKey = secretProtector.Unprotect(config.HashKeyEncrypted); }
        catch { return null; }

        var http = httpClientFactory.CreateClient(HttpClientName);
        http.BaseAddress = new Uri(config.BaseUrl);
        return new EskhataMerchantClient(http, config.CompanyId, hashKey);
    }
}
