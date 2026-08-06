using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Security;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Payments;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

// Eskhata Merchant credentials, org-level (BranchId = null), gated by ManagePaymentGateways.
// UI-prep only: stores/reads credentials so the future Merchant API epic can activate them. The
// Hash key is written encrypted and never returned — GET exposes only HashKeySet.
internal static class EskhataConfigEndpoints
{
    public static void MapEskhataConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("payments/eskhata-config", async (
            StaffAuthorizationService authorizationService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManagePaymentGateways);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var row = await db.EskhataMerchantConfigs.AsNoTracking()
                .SingleOrDefaultAsync(c => c.OrganizationId == orgId && c.BranchId == null, ct);

            return Results.Ok(row is null
                ? new EskhataMerchantConfigDto(string.Empty, string.Empty, 0, false, EskhataMerchantConfigStatus.Inactive)
                : new EskhataMerchantConfigDto(
                    row.BaseUrl,
                    row.CompanyId,
                    row.MerchantId,
                    !string.IsNullOrEmpty(row.HashKeyEncrypted),
                    row.Status));
        });
        // Не помечено AllowPlatformSupportAccess: маршрут содержит "/payments/eskhata" и
        // ловится денежным guard-тестом (PlatformSupportAllowlist_NeverCoversMoneyEndpoints)
        // как совпадение с фрагментом запрета для Eskhata-платежей.

        app.MapPost("payments/eskhata-config", async (
            UpdateEskhataMerchantConfigRequest request,
            StaffAuthorizationService authorizationService,
            ISecretProtector secretProtector,
            IAuditRecordWriter auditRecordWriter,
            TimeProvider timeProvider,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManagePaymentGateways);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var row = await db.EskhataMerchantConfigs.SingleOrDefaultAsync(c => c.OrganizationId == orgId && c.BranchId == null, ct);

            var errors = ValidateRequest(request, hasStoredHashKey: row is not null && !string.IsNullOrEmpty(row.HashKeyEncrypted));
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var now = timeProvider.GetUtcNow();
            if (row is null)
            {
                row = new EskhataMerchantConfigEntity
                {
                    EskhataMerchantConfigId = Guid.NewGuid(),
                    OrganizationId = orgId,
                    BranchId = null,
                    CreatedAtUtc = now
                };
                db.EskhataMerchantConfigs.Add(row);
            }

            row.BaseUrl = request.BaseUrl.Trim().TrimEnd('/');
            row.CompanyId = request.CompanyId.Trim();
            row.MerchantId = request.MerchantId;
            if (!string.IsNullOrWhiteSpace(request.HashKey))
            {
                row.HashKeyEncrypted = secretProtector.Protect(request.HashKey.Trim());
            }
            row.Status = EskhataMerchantConfigStatus.Configured;
            row.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);

            var staff = authorization.StaffContext!;
            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                orgId,
                BranchId: null,
                ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.UpdateEskhataMerchantConfig,
                TargetType: "EskhataMerchantConfig",
                TargetId: orgId.ToString("N"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                // Never log the Hash key: record only whether the secret was rotated this write.
                DetailsJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    request.BaseUrl,
                    request.CompanyId,
                    request.MerchantId,
                    HashKeyRotated = !string.IsNullOrWhiteSpace(request.HashKey)
                })), ct);

            return Results.Ok(new EskhataMerchantConfigDto(
                row.BaseUrl,
                row.CompanyId,
                row.MerchantId,
                !string.IsNullOrEmpty(row.HashKeyEncrypted),
                row.Status));
        });
    }

    private static Dictionary<string, string[]> ValidateRequest(UpdateEskhataMerchantConfigRequest request, bool hasStoredHashKey)
    {
        var errors = new Dictionary<string, string[]>();

        var baseUrl = request.BaseUrl?.Trim() ?? string.Empty;
        if (baseUrl.Length == 0
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors["baseUrl"] = ["Base URL must be an absolute http(s) URL."];
        }

        if (string.IsNullOrWhiteSpace(request.CompanyId))
        {
            errors["companyId"] = ["Company ID is required."];
        }

        if (request.MerchantId <= 0)
        {
            errors["merchantId"] = ["Merchant ID must be a positive number."];
        }

        // Hash key is required on first save; on later saves an empty value keeps the stored secret.
        if (!hasStoredHashKey && string.IsNullOrWhiteSpace(request.HashKey))
        {
            errors["hashKey"] = ["Hash key is required."];
        }

        return errors;
    }
}
