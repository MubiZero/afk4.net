using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.People;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Сетевой запрет человеку — единственное, что платформа решает о живом игроке, а не о клубе.
///
/// Клубное решение остаётся клубным: клуб закрывает у себя карточку, и дальше его слово не идёт.
/// Обратное — клуб, закрывающий человеку вход к конкурентам — и было бы той утечкой контроля,
/// ради которой личность отделена от клубного счёта.
///
/// Человека здесь находят по точному номеру и никак иначе: список людей сети — это ровно то,
/// чего в панели платформы быть не должно.
/// </summary>
public static class PlatformPeopleEndpoints
{
    private const int MaxReasonLength = 512;

    public static void MapPlatformPeopleEndpoints(this WebApplication app)
    {
        app.MapPost("/api/platform/people/lookup", async (
            NetworkPersonLookupRequest request,
            PlatformAdminAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageNetworkBans);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var normalized = PhoneNumberNormalizer.Normalize(request.PhoneNumber);
            if (normalized is null)
            {
                return Results.BadRequest(new { error = "invalid_phone" });
            }

            var phoneNumber = "+" + normalized;
            var person = await dbContext.PlatformPersons
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.PhoneNumber == phoneNumber, cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter, authorization, AuditActionNames.LookupNetworkPerson,
                person?.PlatformPersonId, person is null ? AuditOutcome.Denied : AuditOutcome.Succeeded,
                new { Phone = phoneNumber }, cancellationToken);

            return person is null ? Results.NotFound() : Results.Ok(ToDto(person));
        });

        app.MapPost("/api/platform/people/{platformPersonId:guid}/network-ban", async (
            Guid platformPersonId,
            SetNetworkBanRequest request,
            PlatformAdminAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageNetworkBans);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter, authorization, AuditActionNames.SetNetworkBan, platformPersonId,
                    AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // Причина обязательна: через месяц запрет без неё некому объяснить и не на каком
            // основании снять — а снимать его придётся живому человеку у стойки.
            var reason = request.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length > MaxReasonLength)
            {
                return Results.BadRequest(new { error = "invalid_reason" });
            }

            var person = await dbContext.PlatformPersons.SingleOrDefaultAsync(
                candidate => candidate.PlatformPersonId == platformPersonId, cancellationToken);
            if (person is null) return Results.NotFound();

            // Повторный запрет не переписывает дату: «под запретом с 20 августа» — это факт, а не
            // последнее нажатие кнопки.
            if (person.NetworkBanAtUtc is null)
            {
                var now = timeProvider.GetUtcNow();
                person.NetworkBanAtUtc = now;
                person.NetworkBanReason = reason;
                person.UpdatedAtUtc = now;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await WriteAuditAsync(
                auditRecordWriter, authorization, AuditActionNames.SetNetworkBan, platformPersonId,
                AuditOutcome.Succeeded, new { Reason = person.NetworkBanReason }, cancellationToken);

            return Results.Ok(ToDto(person));
        });

        app.MapDelete("/api/platform/people/{platformPersonId:guid}/network-ban", async (
            Guid platformPersonId,
            PlatformAdminAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageNetworkBans);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter, authorization, AuditActionNames.LiftNetworkBan, platformPersonId,
                    AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var person = await dbContext.PlatformPersons.SingleOrDefaultAsync(
                candidate => candidate.PlatformPersonId == platformPersonId, cancellationToken);
            if (person is null) return Results.NotFound();

            var liftedReason = person.NetworkBanReason;
            if (person.NetworkBanAtUtc is not null)
            {
                person.NetworkBanAtUtc = null;
                person.NetworkBanReason = null;
                person.UpdatedAtUtc = timeProvider.GetUtcNow();
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Причина уходит из карточки, но остаётся в аудите: снятый запрет не должен выглядеть
            // так, будто его никогда не было.
            await WriteAuditAsync(
                auditRecordWriter, authorization, AuditActionNames.LiftNetworkBan, platformPersonId,
                AuditOutcome.Succeeded, new { LiftedReason = liftedReason }, cancellationToken);

            return Results.Ok(ToDto(person));
        });
    }

    private static NetworkPersonDto ToDto(PlatformPersonEntity person) =>
        new(person.PlatformPersonId,
            person.PhoneNumber,
            person.DisplayName,
            person.CreatedAtUtc,
            person.NetworkBanAtUtc,
            person.NetworkBanReason);

    private static Task WriteAuditAsync(
        IAuditRecordWriter auditRecordWriter,
        PlatformAdminAuthorizationResult authorization,
        string action,
        Guid? platformPersonId,
        string outcome,
        object details,
        CancellationToken cancellationToken) =>
        WritePlatformAuditAsync(
            auditRecordWriter,
            Guid.Empty,
            authorization.PlatformAdminContext?.PlatformAdminUserId,
            action,
            "PlatformPerson",
            platformPersonId?.ToString("D"),
            outcome,
            details,
            cancellationToken);
}
