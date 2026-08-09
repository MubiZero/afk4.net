using System.Data;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Offboarding;

public sealed record PurgeResult(PurgeOrganizationResultDto? Value, string? ErrorCode)
{
    public bool Succeeded => ErrorCode is null;

    public static PurgeResult Ok(PurgeOrganizationResultDto value) => new(value, null);

    public static PurgeResult Fail(string errorCode) => new(null, errorCode);
}

/// <summary>
/// Стирание клуба. Разделительная черта — ЧЬИ это данные: персональное и операционное клуба
/// уходит, финансовые отношения клуба с платформой и след действий платформы остаются. После
/// стирания клуб — строка в архиве: было такое заведение, платило столько-то, ушло тогда-то.
///
/// Всё в одной транзакции и в порядке от листьев к корню: половина стирания страшнее, чем его
/// отсутствие — данные вроде бы удалены, но ссылки на них живы.
/// </summary>
public sealed class OrganizationPurgeService(PlatformDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<PurgeResult> PurgeAsync(
        Guid organizationId,
        string confirmationSlug,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return PurgeResult.Fail(OffboardingErrorCodes.NotDeletionPending);
        }

        if (organization.Status == OrganizationStatusNames.Purged)
        {
            return PurgeResult.Fail(OffboardingErrorCodes.OrganizationPurged);
        }

        if (organization.Status != OrganizationStatusNames.DeletionPending)
        {
            return PurgeResult.Fail(OffboardingErrorCodes.NotDeletionPending);
        }

        var now = timeProvider.GetUtcNow();
        if (organization.PurgeEligibleAtUtc is null || organization.PurgeEligibleAtUtc > now)
        {
            // Срок — не формальность: это время, за которое клуб успевает передумать и забрать
            // выгрузку. Стереть раньше значит отнять у него обе возможности.
            return PurgeResult.Fail(OffboardingErrorCodes.PurgeNotDue);
        }

        if (!string.Equals(confirmationSlug?.Trim(), organization.Slug, StringComparison.Ordinal))
        {
            return PurgeResult.Fail(OffboardingErrorCodes.SlugMismatch);
        }

        return await ExecuteAsync(organization, now, cancellationToken);
    }

    private async Task<PurgeResult> ExecuteAsync(
        OrganizationEntity organization,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var organizationId = organization.OrganizationId;

        if (!dbContext.Database.IsRelational())
        {
            var counted = await DeleteEverythingAsync(organizationId, cancellationToken);
            Finalize(organization, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return PurgeResult.Ok(counted);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var result = await DeleteEverythingAsync(organizationId, cancellationToken);
        Finalize(organization, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PurgeResult.Ok(result);
    }

    private static void Finalize(OrganizationEntity organization, DateTimeOffset now)
    {
        organization.Status = OrganizationStatusNames.Purged;
        organization.PurgedAtUtc = now;
        // Срок больше ничего не значит: стирать нечего.
        organization.PurgeEligibleAtUtc = null;
        organization.StatusChangedAtUtc = now;
        organization.UpdatedAtUtc = now;
    }

    /// <summary>
    /// Порядок — от листьев к корню: дочерние строки прежде родительских, устройства и филиалы в
    /// самом конце, потому что на них ссылается почти всё остальное.
    /// </summary>
    private async Task<PurgeOrganizationResultDto> DeleteEverythingAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var deviceIds = await dbContext.Devices
            .Where(device => device.OrganizationId == organizationId)
            .Select(device => device.DeviceId)
            .ToArrayAsync(cancellationToken);
        var staffUserIds = await dbContext.StaffUsers
            .Where(staff => staff.OrganizationId == organizationId)
            .Select(staff => staff.StaffUserId)
            .ToArrayAsync(cancellationToken);

        // Строки чеков и заказов не знают своей организации — они находятся только через родителя.
        var saleIds = await dbContext.PosSales
            .Where(sale => sale.OrganizationId == organizationId)
            .Select(sale => sale.PosSaleId)
            .ToArrayAsync(cancellationToken);
        var shopOrderIds = await dbContext.ShopOrders
            .Where(order => order.OrganizationId == organizationId)
            .Select(order => order.ShopOrderId)
            .ToArrayAsync(cancellationToken);

        await DeleteAsync(dbContext.PosSaleLines.Where(line => saleIds.Contains(line.PosSaleId)), cancellationToken);
        await DeleteAsync(dbContext.ShopOrderLines.Where(line => shopOrderIds.Contains(line.ShopOrderId)), cancellationToken);
        await DeleteAsync(dbContext.DeviceCommands.Where(command => deviceIds.Contains(command.DeviceId)), cancellationToken);
        var deviceIdsNullable = deviceIds.Select(id => (Guid?)id).ToArray();
        await DeleteAsync(dbContext.UpdateRolloutTargets.Where(target => deviceIdsNullable.Contains(target.DeviceId)), cancellationToken);

        // Уведомления привязаны к сотруднику или к организации: адреса почты и телефонов — те же
        // персональные данные, что и учётные записи.
        var staffUserIdsNullable = staffUserIds.Select(id => (Guid?)id).ToArray();
        await DeleteAsync(dbContext.NotificationPreferences.Where(preference => staffUserIdsNullable.Contains(preference.StaffUserId)), cancellationToken);
        var outboxIds = await dbContext.NotificationOutbox
            .Where(row => row.OrganizationId == organizationId)
            .Select(row => row.NotificationOutboxId)
            .ToArrayAsync(cancellationToken);
        await DeleteAsync(dbContext.NotificationOutboxAttachments.Where(attachment => outboxIds.Contains(attachment.NotificationOutboxId)), cancellationToken);
        await DeleteAsync(dbContext.NotificationOutbox.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.AnnouncementReads.Where(read => staffUserIds.Contains(read.StaffUserId)), cancellationToken);

        var sales = await DeleteByOrganizationAsync(organizationId, cancellationToken);
        var players = await DeletePlayersAsync(organizationId, cancellationToken);
        var staff = await DeleteStaffAsync(organizationId, cancellationToken);
        var sessions = await DeleteSessionsAsync(organizationId, cancellationToken);
        var devices = await DeleteDevicesAsync(organizationId, cancellationToken);

        // Аудит режется по актору: одна таблица держит два журнала. Действия сотрудников клуба —
        // это персональные данные и уходят с людьми; действия платформы обязаны пережить стирание,
        // иначе исчезнет и запись о самом стирании.
        var clubAudit = await DeleteAsync(dbContext.AuditRecords.Where(record => record.OrganizationId == organizationId && record.ActorPlatformAdminUserId == null), cancellationToken);

        var branches = await DeleteTopologyAsync(organizationId, cancellationToken);

        return new PurgeOrganizationResultDto(players, staff, sessions, sales, devices, branches, clubAudit);
    }

    private async Task<int> DeleteByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var sales = await DeleteAsync(dbContext.PosSales.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.ShopOrders.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.Receipts.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.Payments.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.PaymentIntents.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.CashMovements.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.Shifts.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.StockMovements.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.ProductBarcodes.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.PosProducts.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.PosProductCategories.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.BillingCommandIdempotency.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.MoneyActionRequests.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.OutboxMessages.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.NewsItems.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.UploadedMedia.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.ReportSchedules.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.OrganizationOwnerInvites.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.OrganizationFeatureOverrides.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.OrganizationLoyaltySettings.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.EskhataMerchantConfigs.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.DcPayLinkConfigs.Where(row => row.OrganizationId == organizationId), cancellationToken);
        return sales;
    }

    private async Task<int> DeletePlayersAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await DeleteAsync(dbContext.LedgerEntries.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.PlayerPackages.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.PlayerAccessTokens.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.PlayerRefreshTokens.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.PlayerCredentials.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.PackageDefinitions.Where(row => row.OrganizationId == organizationId), cancellationToken);
        return await DeleteAsync(dbContext.PlayerAccounts.Where(row => row.OrganizationId == organizationId), cancellationToken);
    }

    private async Task<int> DeleteStaffAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await DeleteAsync(dbContext.StaffAccessTokens.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.StaffRefreshTokens.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.StaffRoleAssignments.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.StaffMoneyCaps.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.StaffInvites.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.StaffPhoneOtps.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.PasswordResetTokens.Where(row => row.OrganizationId == organizationId), cancellationToken);
        return await DeleteAsync(dbContext.StaffUsers.Where(row => row.OrganizationId == organizationId), cancellationToken);
    }

    private async Task<int> DeleteSessionsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await DeleteAsync(dbContext.SessionEvents.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.SessionLeases.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.SessionCommandIdempotency.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.Reservations.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.Tariffs.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.TariffVersions.Where(row => row.OrganizationId == organizationId), cancellationToken);
        return await DeleteAsync(dbContext.Sessions.Where(row => row.OrganizationId == organizationId), cancellationToken);
    }

    private async Task<int> DeleteDevicesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await DeleteAsync(dbContext.DeviceInstalledApps.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.DeviceUpdateStatuses.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.DeviceSeatAssignments.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.DeviceCredentials.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.DeviceEnrollmentCodes.Where(row => row.OrganizationId == organizationId), cancellationToken);
        return await DeleteAsync(dbContext.Devices.Where(row => row.OrganizationId == organizationId), cancellationToken);
    }

    private async Task<int> DeleteTopologyAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await DeleteAsync(dbContext.Seats.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.Walls.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.Zones.Where(row => row.OrganizationId == organizationId), cancellationToken);
        await DeleteAsync(dbContext.BranchDailySnapshots.Where(row => row.OrganizationId == organizationId), cancellationToken);
        return await DeleteAsync(dbContext.Branches.Where(row => row.OrganizationId == organizationId), cancellationToken);
    }

    /// <summary>
    /// Удаление идёт через загрузку и <c>RemoveRange</c>, а не через <c>ExecuteDelete</c>. Второй
    /// быстрее, но не поддерживается провайдером, на котором живёт основная масса тестов, — и тогда
    /// путь стирания в тестах и в проде был бы РАЗНЫМ кодом. Для операции, которая случается с
    /// клубом один раз в жизни, скорость дешевле уверенности.
    /// </summary>
    private async Task<int> DeleteAsync<TEntity>(IQueryable<TEntity> query, CancellationToken cancellationToken)
        where TEntity : class
    {
        var rows = await query.ToArrayAsync(cancellationToken);
        if (rows.Length == 0)
        {
            return 0;
        }

        dbContext.Set<TEntity>().RemoveRange(rows);
        // Сохраняем сразу: порядок удаления выстроен от листьев к корню, и внешние ключи в
        // настоящей базе проверяются на каждом шаге, а не в конце.
        await dbContext.SaveChangesAsync(cancellationToken);
        return rows.Length;
    }
}
