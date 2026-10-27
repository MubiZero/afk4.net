using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Ключи ПК администраторов для офлайн-аренд (docs/roadmap/offline-front-desk.md). Регистрирует Панель
/// сама, пока связь есть, — тот, кто сажает гостей, тот и отвечает за ключ своего ПК. Отзывает
/// управляющий: после отзыва агенты филиала с ближайшим сердцебиением перестают ему верить.
/// </summary>
internal static partial class OfflineLeaseKeyEndpoints
{
    /// <summary>Действующих ключей в филиале не больше: у стойки два-три ПК, а не двадцать.</summary>
    public const int MaxActiveKeysPerBranch = 20;

    public static void MapOfflineLeaseKeyEndpoints(this IEndpointRouteBuilder organizations)
    {
        organizations.MapPost("branches/{branchId:guid}/offline-lease-keys", async (
            Guid branchId, RegisterOfflineLeaseKeyRequest request, StaffAuthorizationService authorizationService,
            PlatformDbContext db, IAuditRecordWriter audit, TimeProvider clock, CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(branchId, OrganizationPermissionNames.StartSession, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!KeyIdPattern().IsMatch(request.KeyId ?? string.Empty) || !IsP256PublicKey(request.PublicKeyPem))
            {
                return Refusal(OfflineLeaseKeyErrorCodeNames.Invalid, "Key id or public key is not acceptable.", StatusCodes.Status400BadRequest);
            }

            var staff = authorization.StaffContext!;
            var existing = await db.OfflineLeaseKeys.SingleOrDefaultAsync(key => key.BranchId == branchId && key.KeyId == request.KeyId, ct);
            if (existing is not null)
            {
                if (existing.RevokedAtUtc is not null)
                {
                    return Refusal(OfflineLeaseKeyErrorCodeNames.Revoked, "This key was revoked; create a new one.", StatusCodes.Status409Conflict);
                }

                // Повтор той же регистрации после обрыва связи — не ошибка.
                return Normalize(existing.PublicKeyPem) == Normalize(request.PublicKeyPem)
                    ? Results.Ok(Dto(existing))
                    : Refusal(OfflineLeaseKeyErrorCodeNames.Conflict, "Another key already uses this id.", StatusCodes.Status409Conflict);
            }

            if (await db.OfflineLeaseKeys.CountAsync(key => key.BranchId == branchId && key.RevokedAtUtc == null, ct) >= MaxActiveKeysPerBranch)
            {
                return Refusal(OfflineLeaseKeyErrorCodeNames.Limit, "Too many active keys in this branch.", StatusCodes.Status409Conflict);
            }

            var entity = new OfflineLeaseKeyEntity
            {
                OfflineLeaseKeyId = Guid.NewGuid(),
                OrganizationId = staff.OrganizationId,
                BranchId = branchId,
                KeyId = request.KeyId!,
                PublicKeyPem = request.PublicKeyPem.Trim(),
                RegisteredByStaffUserId = staff.StaffUserId,
                RegisteredAtUtc = clock.GetUtcNow()
            };
            db.OfflineLeaseKeys.Add(entity);
            await db.SaveChangesAsync(ct);
            await WriteAuditAsync(audit, entity, staff.StaffUserId, AuditActionNames.RegisterOfflineLeaseKey, ct);
            return Results.Ok(Dto(entity));
        });

        organizations.MapGet("branches/{branchId:guid}/offline-lease-keys", async (
            Guid branchId, StaffAuthorizationService authorizationService, PlatformDbContext db, CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(branchId, OrganizationPermissionNames.ManageBranchSettings, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var keys = await db.OfflineLeaseKeys.AsNoTracking()
                .Where(key => key.BranchId == branchId && key.OrganizationId == authorization.StaffContext!.OrganizationId)
                .OrderByDescending(key => key.RegisteredAtUtc)
                .ToListAsync(ct);
            return Results.Ok(keys.Select(Dto).ToList());
        });

        organizations.MapDelete("branches/{branchId:guid}/offline-lease-keys/{keyId}", async (
            Guid branchId, string keyId, StaffAuthorizationService authorizationService,
            PlatformDbContext db, IAuditRecordWriter audit, TimeProvider clock, CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(branchId, OrganizationPermissionNames.ManageBranchSettings, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var key = await db.OfflineLeaseKeys.SingleOrDefaultAsync(
                row => row.BranchId == branchId && row.KeyId == keyId && row.OrganizationId == authorization.StaffContext!.OrganizationId, ct);
            if (key is null) return Results.NotFound();
            if (key.RevokedAtUtc is null)
            {
                key.RevokedAtUtc = clock.GetUtcNow();
                await db.SaveChangesAsync(ct);
                await WriteAuditAsync(audit, key, authorization.StaffContext!.StaffUserId, AuditActionNames.RevokeOfflineLeaseKey, ct);
            }

            return Results.NoContent();
        });
    }

    private static bool IsP256PublicKey(string? pem)
    {
        if (string.IsNullOrWhiteSpace(pem) || pem.Length > 1024)
        {
            return false;
        }

        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(pem);
            return key.KeySize == 256;
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private static string Normalize(string pem) => pem.Replace("\r", string.Empty, StringComparison.Ordinal).Trim();

    private static BranchOfflineLeaseKeyDto Dto(OfflineLeaseKeyEntity key) =>
        new(key.KeyId, key.RegisteredByStaffUserId, key.RegisteredAtUtc, key.RevokedAtUtc);

    private static IResult Refusal(string code, string error, int status) =>
        Results.Json(new { error, code }, statusCode: status);

    private static Task WriteAuditAsync(IAuditRecordWriter audit, OfflineLeaseKeyEntity key, Guid actor, string action, CancellationToken ct) =>
        audit.WriteAsync(new AuditRecordWriteRequest(
            key.OrganizationId, key.BranchId, actor, action, "OfflineLeaseKey", key.KeyId,
            AuditOutcome.Succeeded, "PlatformApi", JsonSerializer.Serialize(new { key.KeyId })), ct);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex KeyIdPattern();
}
