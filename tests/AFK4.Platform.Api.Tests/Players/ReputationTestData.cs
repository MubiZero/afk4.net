using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Players;

/// <summary>Личность, её счета в клубах и её суточный снимок — заготовка для тестов репутации.</summary>
internal static class ReputationTestData
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-20T09:00:00Z");

    /// <summary>Момент, на который посчитан снимок: вчерашняя ночь, как в проде.</summary>
    public static readonly DateTimeOffset SnapshotAt = DateTimeOffset.Parse("2026-08-20T03:00:00Z");

    public static async Task<Guid> AddPersonAsync(
        PlatformApiFactory factory,
        string phoneNumber,
        bool networkBanned = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var personId = Guid.NewGuid();
        db.PlatformPersons.Add(new PlatformPersonEntity
        {
            PlatformPersonId = personId,
            PhoneNumber = phoneNumber,
            DisplayName = "Фаррух",
            PhoneVerifiedAtUtc = Now,
            NetworkBanAtUtc = networkBanned ? Now : null,
            NetworkBanReason = networkBanned ? "Порча оборудования" : null,
            IsActive = true,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return personId;
    }

    public static async Task<Guid> AddAccountAsync(
        PlatformApiFactory factory,
        Guid organizationId,
        Guid branchId,
        Guid? platformPersonId,
        string? phoneNumber = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var accountId = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = accountId,
            OrganizationId = organizationId,
            HomeBranchId = branchId,
            PlatformPersonId = platformPersonId,
            PhoneNumber = phoneNumber,
            DisplayName = "Карточка клуба",
            IsActive = true,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return accountId;
    }

    /// <summary>Соседний клуб — тот самый, чью клиентуру нельзя вычислить.</summary>
    public static async Task<(Guid OrganizationId, Guid BranchId)> AddOtherClubAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Name = "Соседний клуб",
            CreatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Name = "Соседний клуб — филиал",
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return (organizationId, branchId);
    }

    /// <summary>Заявка, записанная на стойке с одного телефона: счёта у гостя ещё нет.</summary>
    public static async Task AddLiveRequestByPhoneAsync(
        PlatformApiFactory factory,
        Guid organizationId,
        Guid branchId,
        string phoneNumber,
        string state = ReservationStateNames.Pending)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Reservations.Add(new ReservationEntity
        {
            ReservationId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            PlayerAccountId = null,
            CustomerName = "Гость по телефону",
            PhoneNumber = phoneNumber,
            StartsAtUtc = Now.AddHours(2),
            EndsAtUtc = Now.AddHours(3),
            State = state,
            Source = ReservationSourceNames.Operator,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    public static async Task AddSnapshotAsync(
        PlatformApiFactory factory,
        Guid platformPersonId,
        int visits,
        int noShows,
        DateTimeOffset? calculatedAtUtc = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.PlatformReputationSnapshots.Add(new PlatformReputationSnapshotEntity
        {
            PlatformPersonId = platformPersonId,
            NetworkVisits = visits,
            NetworkNoShows = noShows,
            CalculatedAtUtc = calculatedAtUtc ?? SnapshotAt
        });
        await db.SaveChangesAsync();
    }

    public static async Task AddEndedSessionAsync(
        PlatformApiFactory factory,
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            PlayerKind = "account",
            PlayerAccountId = playerAccountId,
            State = SessionStateNames.Ended,
            RequestedAtUtc = Now.AddDays(-1),
            StartedAtUtc = Now.AddDays(-1),
            EndedAtUtc = Now.AddDays(-1).AddHours(2),
            UpdatedAtUtc = Now.AddDays(-1).AddHours(2)
        });
        await db.SaveChangesAsync();
    }

    public static string ReputationRoute(Guid platformPersonId) =>
        $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/players/reputation/{platformPersonId:D}";

    public static string LookupRoute() =>
        $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/players/reputation/lookup";
}
