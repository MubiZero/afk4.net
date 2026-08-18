using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

public sealed class EfPlayerClubAccountResolver(PlatformDbContext dbContext) : IPlayerClubAccountResolver
{
    public async Task<PlayerClubSelection> ResolveAsync(
        Guid platformPersonId,
        Guid? requestedOrganizationId,
        Guid? pinnedOrganizationId,
        CancellationToken cancellationToken)
    {
        // 1. Клуб, названный запросом. Если счёта в нём нет — клуб всё равно этот: молча
        //    подставить другой значит показать человеку чужой кошелёк там, где он ждал свой.
        if (requestedOrganizationId is { } requested)
        {
            return new PlayerClubSelection(
                requested, await FindAsync(platformPersonId, requested, cancellationToken));
        }

        // 2. Клуб, закреплённый в токене при входе, — дорога старых клиентов.
        if (pinnedOrganizationId is { } pinned)
        {
            return new PlayerClubSelection(
                pinned, await FindAsync(platformPersonId, pinned, cancellationToken));
        }

        // 3. Единственная связь не нуждается в выборе.
        var accounts = await dbContext.PlayerAccounts
            .AsNoTracking()
            .Where(account => account.PlatformPersonId == platformPersonId && account.IsActive)
            .OrderBy(account => account.CreatedAtUtc)
            .Take(2)
            .ToListAsync(cancellationToken);

        // 4. Клубов несколько и ни один не назван — выбирать за человека нечего.
        return accounts.Count == 1
            ? new PlayerClubSelection(accounts[0].OrganizationId, accounts[0])
            : PlayerClubSelection.None;
    }

    private Task<PlayerAccountEntity?> FindAsync(
        Guid platformPersonId,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        dbContext.PlayerAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                account => account.PlatformPersonId == platformPersonId
                    && account.OrganizationId == organizationId
                    && account.IsActive,
                cancellationToken);
}
