using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Friends;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Friends;

/// <summary>
/// Друзья и «я сейчас в зале».
///
/// Дружба живёт на личности, а не на клубной карточке: друг остаётся другом в любом клубе сети.
/// Присутствие считается из живых сессий и показывается **только принятым друзьям** — и только
/// если человек сам этого не запретил.
/// </summary>
public sealed class EfFriendService(PlatformDbContext dbContext, TimeProvider timeProvider) : IFriendService
{
    public async Task<FriendsDto> ListAsync(Guid personId, CancellationToken ct)
    {
        var rows = await dbContext.PersonFriendships.AsNoTracking()
            .Where(friendship => friendship.RequesterPersonId == personId
                || friendship.AddresseePersonId == personId)
            .ToListAsync(ct);

        var accepted = rows.Where(row => row.State == FriendshipStateNames.Accepted).ToList();
        var incoming = rows
            .Where(row => row.State == FriendshipStateNames.Pending && row.AddresseePersonId == personId)
            .OrderByDescending(row => row.CreatedAtUtc)
            .ToList();
        var outgoing = rows
            .Where(row => row.State == FriendshipStateNames.Pending && row.RequesterPersonId == personId)
            .OrderByDescending(row => row.CreatedAtUtc)
            .ToList();

        var otherIds = rows
            .Select(row => Other(row, personId))
            .Distinct()
            .ToList();
        var people = await dbContext.PlatformPersons.AsNoTracking()
            .Where(person => otherIds.Contains(person.PlatformPersonId))
            .Select(person => new
            {
                person.PlatformPersonId,
                person.DisplayName,
                person.ShowsPresenceToFriends
            })
            .ToDictionaryAsync(person => person.PlatformPersonId, ct);

        var friendIds = accepted.Select(row => Other(row, personId)).ToList();
        var presence = await PresenceAsync(
            friendIds.Where(id => people.GetValueOrDefault(id)?.ShowsPresenceToFriends ?? false).ToList(), ct);

        string NameOf(Guid id) => people.GetValueOrDefault(id)?.DisplayName ?? string.Empty;

        var me = await dbContext.PlatformPersons.AsNoTracking()
            .Where(person => person.PlatformPersonId == personId)
            .Select(person => person.ShowsPresenceToFriends)
            .FirstOrDefaultAsync(ct);

        return new FriendsDto(
            accepted
                .Select(row => Other(row, personId))
                .Select(id => new FriendDto(id, NameOf(id), presence.GetValueOrDefault(id)))
                // Кто сейчас в зале — первым: ради этого список и открывают.
                .OrderByDescending(friend => friend.Presence is not null)
                .ThenBy(friend => friend.DisplayName)
                .ToList(),
            incoming
                .Select(row => new FriendRequestDto(
                    row.PersonFriendshipId, row.RequesterPersonId, NameOf(row.RequesterPersonId), row.CreatedAtUtc))
                .ToList(),
            outgoing
                .Select(row => new FriendRequestDto(
                    row.PersonFriendshipId, row.AddresseePersonId, NameOf(row.AddresseePersonId), row.CreatedAtUtc))
                .ToList(),
            me);
    }

    /// <summary>
    /// Позвать в друзья по номеру.
    /// </summary>
    /// <remarks>
    /// Ответ на чужой номер одинаков всегда — заявка ушла. Незарегистрированный номер, номер
    /// человека, который уже отказал, и номер того, кто просто ещё не ответил, снаружи выглядят
    /// одинаково: иначе приложение стало бы способом проверять, есть ли номер в сети.
    /// Единственный отличимый отказ — собственный номер: тут утечки нет, а молчание выглядело бы
    /// как поломка.
    /// </remarks>
    public async Task<FriendActionResult> RequestAsync(Guid personId, string phoneNumber, CancellationToken ct)
    {
        // Канонический вид у личности — «+<цифры>»: тот же, в котором её заводит регистрация.
        var digits = PhoneNumberNormalizer.Normalize(phoneNumber);
        if (digits is null)
        {
            return FriendActionResult.Ok();
        }
        var normalized = "+" + digits;

        var me = await dbContext.PlatformPersons.AsNoTracking()
            .Where(person => person.PlatformPersonId == personId)
            .Select(person => person.PhoneNumber)
            .FirstOrDefaultAsync(ct);
        if (me is not null && string.Equals(me, normalized, StringComparison.Ordinal))
        {
            return FriendActionResult.Refused(FriendRefusalCodes.Self);
        }

        var target = await dbContext.PlatformPersons.AsNoTracking()
            .Where(person => person.PhoneNumber == normalized && person.IsActive)
            .Select(person => person.PlatformPersonId)
            .FirstOrDefaultAsync(ct);
        if (target == Guid.Empty)
        {
            return FriendActionResult.Ok();
        }

        var existing = await FindAsync(personId, target, ct);
        if (existing is not null)
        {
            // Уже друзья, уже позвали, уже отказали — снаружи всё это «заявка ушла». Внутри не
            // меняем ничего: отказавшего человека второй раз не зовут.
            return FriendActionResult.Ok();
        }

        dbContext.PersonFriendships.Add(new PersonFriendshipEntity
        {
            PersonFriendshipId = Guid.NewGuid(),
            RequesterPersonId = personId,
            AddresseePersonId = target,
            State = FriendshipStateNames.Pending,
            CreatedAtUtc = timeProvider.GetUtcNow()
        });

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Два нажатия подряд — вторая заявка не нужна, а ответ тот же.
            dbContext.ChangeTracker.Clear();
        }

        return FriendActionResult.Ok();
    }

    public async Task<FriendActionResult> AcceptAsync(Guid personId, Guid friendRequestId, CancellationToken ct)
    {
        var request = await dbContext.PersonFriendships
            .FirstOrDefaultAsync(friendship => friendship.PersonFriendshipId == friendRequestId
                // Принять можно только заявку, пришедшую тебе: чужую — даже свою отправленную —
                // принимать за другого нельзя.
                && friendship.AddresseePersonId == personId
                && friendship.State == FriendshipStateNames.Pending, ct);
        if (request is null)
        {
            return FriendActionResult.Refused(FriendRefusalCodes.NoSuchRequest);
        }

        request.State = FriendshipStateNames.Accepted;
        request.RespondedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(ct);
        return FriendActionResult.Ok();
    }

    public async Task<FriendActionResult> DeclineAsync(Guid personId, Guid friendRequestId, CancellationToken ct)
    {
        var request = await dbContext.PersonFriendships
            .FirstOrDefaultAsync(friendship => friendship.PersonFriendshipId == friendRequestId
                && friendship.AddresseePersonId == personId
                && friendship.State == FriendshipStateNames.Pending, ct);
        if (request is null)
        {
            return FriendActionResult.Refused(FriendRefusalCodes.NoSuchRequest);
        }

        request.State = FriendshipStateNames.Declined;
        request.RespondedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(ct);
        return FriendActionResult.Ok();
    }

    /// <summary>
    /// Убрать из друзей. Строка удаляется целиком, а не помечается отказом: человек не отказывал,
    /// он расстался — и позвать друг друга снова должно быть можно.
    /// </summary>
    public async Task<FriendActionResult> RemoveAsync(Guid personId, Guid friendPersonId, CancellationToken ct)
    {
        var friendship = await FindAsync(personId, friendPersonId, ct);
        if (friendship is null || friendship.State != FriendshipStateNames.Accepted)
        {
            return FriendActionResult.Refused(FriendRefusalCodes.NoSuchRequest);
        }

        dbContext.PersonFriendships.Remove(friendship);
        await dbContext.SaveChangesAsync(ct);
        return FriendActionResult.Ok();
    }

    public async Task<FriendActionResult> SetPresenceVisibilityAsync(
        Guid personId, bool showsPresence, CancellationToken ct)
    {
        var person = await dbContext.PlatformPersons
            .FirstOrDefaultAsync(candidate => candidate.PlatformPersonId == personId, ct);
        if (person is null)
        {
            return FriendActionResult.Refused(FriendRefusalCodes.NoSuchRequest);
        }

        person.ShowsPresenceToFriends = showsPresence;
        person.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(ct);
        return FriendActionResult.Ok();
    }

    /// <summary>
    /// Кто из этих людей сейчас за ПК: клуб и зал. Сессия ищется по всем клубным карточкам
    /// человека — друг мог сесть в клубе, в котором меня нет вовсе.
    /// </summary>
    private async Task<Dictionary<Guid, FriendPresenceDto>> PresenceAsync(
        IReadOnlyList<Guid> personIds, CancellationToken ct)
    {
        if (personIds.Count == 0) return [];

        var live = await (
            from session in dbContext.Sessions.AsNoTracking()
            join account in dbContext.PlayerAccounts.AsNoTracking()
                on session.PlayerAccountId equals account.PlayerAccountId
            join branch in dbContext.Branches.AsNoTracking() on session.BranchId equals branch.BranchId
            join organization in dbContext.Organizations.AsNoTracking()
                on session.OrganizationId equals organization.OrganizationId
            where account.PlatformPersonId != null
                && personIds.Contains(account.PlatformPersonId!.Value)
                && (session.State == SessionStateNames.Active || session.State == SessionStateNames.Paused)
            select new
            {
                PersonId = account.PlatformPersonId!.Value,
                OrganizationName = organization.Name,
                BranchName = branch.Name
            }).ToListAsync(ct);

        return live
            .GroupBy(row => row.PersonId)
            .ToDictionary(
                group => group.Key,
                group => new FriendPresenceDto(group.First().OrganizationName, group.First().BranchName));
    }

    private Task<PersonFriendshipEntity?> FindAsync(Guid first, Guid second, CancellationToken ct) =>
        dbContext.PersonFriendships.FirstOrDefaultAsync(
            friendship =>
                (friendship.RequesterPersonId == first && friendship.AddresseePersonId == second) ||
                (friendship.RequesterPersonId == second && friendship.AddresseePersonId == first),
            ct);

    private static Guid Other(PersonFriendshipEntity friendship, Guid personId) =>
        friendship.RequesterPersonId == personId ? friendship.AddresseePersonId : friendship.RequesterPersonId;
}
