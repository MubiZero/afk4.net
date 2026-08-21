using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Players;

public sealed class EfPlayerClubMembershipService(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IPlayerClubMembershipService
{
    public async Task<PlayerClubMembershipResult> EnsureAsync(
        Guid platformPersonId,
        Guid organizationId,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var existing = await FindAttachedAsync(platformPersonId, organizationId, cancellationToken);
        if (existing is not null)
        {
            // Закрытая карточка остаётся закрытой. Отдать её как рабочую значило бы вернуть
            // человеку ровно то, что клуб у него забрал кнопкой «Деактивировать».
            return existing.IsActive
                ? PlayerClubMembershipResult.Existing(existing)
                : PlayerClubMembershipResult.Refused(PlayerClubMembershipErrors.ClubAccountClosed);
        }

        var person = await dbContext.PlatformPersons
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.PlatformPersonId == platformPersonId && candidate.IsActive,
                cancellationToken);
        if (person is null)
        {
            return PlayerClubMembershipResult.Refused(PlayerClubMembershipErrors.PersonNotFound);
        }

        var organizationExists = await dbContext.Organizations
            .AsNoTracking()
            .AnyAsync(organization => organization.OrganizationId == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return PlayerClubMembershipResult.Refused(PlayerClubMembershipErrors.OrganizationNotFound);
        }

        var homeBranch = await ResolveHomeBranchAsync(organizationId, branchId, cancellationToken);
        if (homeBranch.Error is not null)
        {
            return PlayerClubMembershipResult.Refused(homeBranch.Error);
        }

        // Карточка, которую оператор завёл руками по тому же номеру, — это тот же человек. Завести
        // рядом вторую значит разделить его деньги надвое и заставить стойку гадать, какая живая.
        var counterCard = await FindUnclaimedCounterCardAsync(person.PhoneNumber, organizationId, cancellationToken);
        if (counterCard is not null)
        {
            counterCard.PlatformPersonId = platformPersonId;
            await dbContext.SaveChangesAsync(cancellationToken);
            return PlayerClubMembershipResult.Existing(counterCard);
        }

        // Живой карточки нет, а закрытая по тому же номеру есть — это запрет, а не пустое место.
        // Свежая карточка рядом с ней обошла бы решение клуба через регистрацию в приложении.
        if (await HasClosedCounterCardAsync(person.PhoneNumber, organizationId, cancellationToken))
        {
            return PlayerClubMembershipResult.Refused(PlayerClubMembershipErrors.ClubAccountClosed);
        }

        var now = timeProvider.GetUtcNow();
        var account = new PlayerAccountEntity
        {
            PlayerAccountId = Guid.NewGuid(),
            OrganizationId = organizationId,
            PlatformPersonId = platformPersonId,
            HomeBranchId = homeBranch.BranchId,
            DisplayName = person.DisplayName,
            PhoneNumber = person.PhoneNumber,
            PreferredLocale = person.PreferredLocale,
            IsActive = true,
            CreatedFromApp = true,
            CreatedAtUtc = now
        };
        dbContext.PlayerAccounts.Add(account);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Второй одновременный вызов уже открыл счёт и упёрся в уникальный индекс. Ответ на
            // это — перечитать чужую работу, а не заводить вторую связь.
            dbContext.Entry(account).State = EntityState.Detached;
            var winner = await FindAttachedAsync(platformPersonId, organizationId, cancellationToken);
            if (winner is null)
            {
                // Индекс сработал не на связь — значит сломалось что-то другое, и молчать об этом
                // нельзя.
                throw;
            }

            return winner.IsActive
                ? PlayerClubMembershipResult.Existing(winner)
                : PlayerClubMembershipResult.Refused(PlayerClubMembershipErrors.ClubAccountClosed);
        }

        return PlayerClubMembershipResult.Opened(account);
    }

    private Task<PlayerAccountEntity?> FindAttachedAsync(
        Guid platformPersonId, Guid organizationId, CancellationToken cancellationToken) =>
        dbContext.PlayerAccounts.SingleOrDefaultAsync(
            account => account.PlatformPersonId == platformPersonId
                && account.OrganizationId == organizationId,
            cancellationToken);

    /// <summary>Закрытая ничейная карточка с тем же номером: решение клуба, а не пустое место.</summary>
    private Task<bool> HasClosedCounterCardAsync(
        string phoneNumber, Guid organizationId, CancellationToken cancellationToken) =>
        dbContext.PlayerAccounts
            .AsNoTracking()
            .AnyAsync(
                account => account.OrganizationId == organizationId
                    && account.PlatformPersonId == null
                    && !account.IsActive
                    && account.PhoneNumber == phoneNumber,
                cancellationToken);

    /// <summary>
    /// Ничейная карточка с тем же номером в этом клубе. Если их несколько — берётся та, за которой
    /// человек действительно играл последней, а при равенстве заведённая позже: то же правило, что
    /// у переноса существующих данных, чтобы в системе не жило двух разных ответов на один вопрос.
    /// </summary>
    private async Task<PlayerAccountEntity?> FindUnclaimedCounterCardAsync(
        string phoneNumber, Guid organizationId, CancellationToken cancellationToken)
    {
        var candidates = await dbContext.PlayerAccounts
            .Where(account => account.OrganizationId == organizationId
                && account.PlatformPersonId == null
                && account.IsActive
                && account.PhoneNumber == phoneNumber)
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        var candidateIds = candidates.Select(account => account.PlayerAccountId).ToList();
        var lastSeen = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.PlayerAccountId != null
                && candidateIds.Contains(session.PlayerAccountId!.Value)
                && session.State == SessionStateNames.Ended)
            .GroupBy(session => session.PlayerAccountId!.Value)
            .Select(group => new
            {
                PlayerAccountId = group.Key,
                LastSeen = group.Max(session => session.StartedAtUtc ?? session.RequestedAtUtc)
            })
            .ToDictionaryAsync(row => row.PlayerAccountId, row => row.LastSeen, cancellationToken);

        return candidates
            .OrderByDescending(account => lastSeen.GetValueOrDefault(account.PlayerAccountId))
            .ThenByDescending(account => account.CreatedAtUtc)
            .ThenByDescending(account => account.PlayerAccountId)
            .First();
    }

    private async Task<(Guid BranchId, string? Error)> ResolveHomeBranchAsync(
        Guid organizationId, Guid? branchId, CancellationToken cancellationToken)
    {
        if (branchId is { } requested)
        {
            var known = await dbContext.Branches
                .AsNoTracking()
                .AnyAsync(
                    branch => branch.BranchId == requested && branch.OrganizationId == organizationId,
                    cancellationToken);
            return known
                ? (requested, null)
                : (Guid.Empty, PlayerClubMembershipErrors.BranchNotFound);
        }

        var branches = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId)
            .OrderBy(branch => branch.CreatedAtUtc)
            .Take(2)
            .Select(branch => branch.BranchId)
            .ToListAsync(cancellationToken);

        // Гадать филиал за человека нельзя: он придёт в тот, который выбрал, а счёт окажется в
        // другом, и в отчётах клуба это будет выглядеть как два разных гостя. Клуб вовсе без
        // филиалов назвать тоже нечего — ответ тот же.
        return branches.Count == 1
            ? (branches[0], null)
            : (Guid.Empty, PlayerClubMembershipErrors.BranchRequired);
    }
}
