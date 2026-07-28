using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Security;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Payments;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

// Конфиг приёма DushanbeCity, org-level (BranchId=null), гейт ManagePaymentGateways.
// Номер карты пишется шифрованным и наружу не возвращается — GET отдаёт только CardLast4 + CardSet.
internal static class DcConfigEndpoints
{
    public static void MapDcConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("payments/dc-config", async (
            StaffAuthorizationService authorizationService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManagePaymentGateways);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var row = await db.DcPayLinkConfigs.AsNoTracking()
                .SingleOrDefaultAsync(c => c.OrganizationId == orgId && c.BranchId == null, ct);

            return Results.Ok(row is null
                ? new DcPayLinkConfigDto(false, string.Empty, "AFK4-{ref}", false)
                : new DcPayLinkConfigDto(
                    !string.IsNullOrEmpty(row.ReceivingCardEncrypted),
                    row.CardLast4,
                    row.CommentTemplate,
                    row.IsActive));
        });

        app.MapPost("payments/dc-config", async (
            UpdateDcPayLinkConfigRequest request,
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
            var row = await db.DcPayLinkConfigs.SingleOrDefaultAsync(c => c.OrganizationId == orgId && c.BranchId == null, ct);

            var errors = ValidateRequest(request, hasStoredCard: row is not null && !string.IsNullOrEmpty(row.ReceivingCardEncrypted));
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var now = timeProvider.GetUtcNow();
            if (row is null)
            {
                row = new DcPayLinkConfigEntity
                {
                    DcPayLinkConfigId = Guid.NewGuid(),
                    OrganizationId = orgId,
                    BranchId = null,
                    CreatedAtUtc = now
                };
                db.DcPayLinkConfigs.Add(row);
            }

            if (!string.IsNullOrWhiteSpace(request.CardNumber))
            {
                var digits = new string(request.CardNumber.Where(char.IsDigit).ToArray());
                row.ReceivingCardEncrypted = secretProtector.Protect(digits);
                row.CardLast4 = digits[^4..];
            }
            row.CommentTemplate = string.IsNullOrWhiteSpace(request.CommentTemplate) ? "AFK4-{ref}" : request.CommentTemplate.Trim();
            row.IsActive = request.IsActive;
            row.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);

            var staff = authorization.StaffContext!;
            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                orgId, BranchId: null, ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.UpdateDcPayLinkConfig,
                TargetType: "DcPayLinkConfig", TargetId: orgId.ToString("N"),
                Outcome: AuditOutcome.Succeeded, SourceApp: "PlatformApi",
                // Карту не логируем: только факт ротации + last4.
                DetailsJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    CardRotated = !string.IsNullOrWhiteSpace(request.CardNumber),
                    row.CardLast4, row.CommentTemplate, row.IsActive
                })), ct);

            return Results.Ok(new DcPayLinkConfigDto(
                !string.IsNullOrEmpty(row.ReceivingCardEncrypted), row.CardLast4, row.CommentTemplate, row.IsActive));
        });
    }

    private static Dictionary<string, string[]> ValidateRequest(UpdateDcPayLinkConfigRequest request, bool hasStoredCard)
    {
        var errors = new Dictionary<string, string[]>();
        var digits = new string((request.CardNumber ?? string.Empty).Where(char.IsDigit).ToArray());

        // Карта обязательна на первом сохранении; пустая на последующих — сохраняет прежнюю.
        if (!hasStoredCard && digits.Length < 12)
        {
            errors["cardNumber"] = ["Card number is required (at least 12 digits)."];
        }
        else if (digits.Length > 0 && digits.Length < 12)
        {
            errors["cardNumber"] = ["Card number must have at least 12 digits."];
        }

        if (string.IsNullOrWhiteSpace(request.CommentTemplate) || !request.CommentTemplate.Contains("{ref}", StringComparison.Ordinal))
        {
            errors["commentTemplate"] = ["Comment template must contain {ref}."];
        }

        return errors;
    }
}
