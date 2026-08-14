using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

/// Напоминания, у которых нет события: время само подходит. Проверяем, что игрок узнаёт о конце
/// сессии, пока ещё может продлить, и о брони — пока ещё может выйти из дома.
public sealed class PlayerReminderRunnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid OrgId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid BranchId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid SeatId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid PlayerId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004");

    private static PlatformDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"reminders-{Guid.NewGuid()}")
            .Options;
        var dbContext = new PlatformDbContext(options);

        dbContext.Branches.Add(new BranchEntity
        {
            BranchId = BranchId,
            OrganizationId = OrgId,
            Slug = "main",
            Name = "На Рудаки",
            City = "Душанбе",
            PreferredTimeZone = "Asia/Dushanbe",
            CreatedAtUtc = Now,
        });
        dbContext.Seats.Add(new SeatEntity
        {
            SeatId = SeatId,
            OrganizationId = OrgId,
            BranchId = BranchId,
            ZoneId = Guid.NewGuid(),
            Name = "PC-07",
            SortOrder = 1,
            CreatedAtUtc = Now,
        });
        dbContext.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerId,
            OrganizationId = OrgId,
            HomeBranchId = BranchId,
            DisplayName = "Иван",
            PhoneNumber = "+992900000001",
            IsActive = true,
            CreatedAtUtc = Now,
        });
        dbContext.SaveChanges();
        return dbContext;
    }

    private static void AddSession(
        PlatformDbContext dbContext,
        DateTimeOffset? endsAtUtc,
        string state = SessionStateNames.Active,
        Guid? playerAccountId = null)
    {
        dbContext.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = OrgId,
            BranchId = BranchId,
            SeatId = SeatId,
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = playerAccountId is null ? "guest" : "account",
            PlayerAccountId = playerAccountId,
            State = state,
            RequestedAtUtc = Now.AddHours(-2),
            StartedAtUtc = Now.AddHours(-2),
            EndsAtUtc = endsAtUtc,
        });
        dbContext.SaveChanges();
    }

    private static void AddReservation(
        PlatformDbContext dbContext,
        DateTimeOffset startsAtUtc,
        string state = ReservationStateNames.Confirmed,
        Guid? playerAccountId = null)
    {
        dbContext.Reservations.Add(new ReservationEntity
        {
            ReservationId = Guid.NewGuid(),
            OrganizationId = OrgId,
            BranchId = BranchId,
            SeatId = SeatId,
            PlayerAccountId = playerAccountId ?? PlayerId,
            CustomerName = "Иван",
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = startsAtUtc.AddHours(2),
            State = state,
            Source = "player_app",
            CreatedByStaffUserId = Guid.NewGuid(),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
        });
        dbContext.SaveChanges();
    }

    private static (PlayerReminderRunner Runner, RecordingNotificationService Sent) Build(PlatformDbContext dbContext)
    {
        var sent = new RecordingNotificationService();
        return (new PlayerReminderRunner(dbContext, sent, new FixedTimeProvider(Now)), sent);
    }

    /// Смысл предупреждения — успеть продлить. Опоздавшее уведомление бесполезно.
    [Fact]
    public async Task Run_SessionEndingWithinTenMinutes_NotifiesThePlayer()
    {
        using var dbContext = NewContext();
        AddSession(dbContext, Now.AddMinutes(8), playerAccountId: PlayerId);
        var (runner, sent) = Build(dbContext);

        await runner.RunAsync(CancellationToken.None);

        var request = Assert.Single(sent.Requests);
        Assert.Equal(NotificationTemplateKeys.PlayerSessionEnding, request.TemplateKey);
        Assert.Equal([NotificationChannel.Push], request.PreferredChannels);
        Assert.Equal(PlayerId, request.Recipient.PlayerAccountId);
        Assert.Equal("8", request.Tokens["minutes"]);
        Assert.Equal("PC-07", request.Tokens["seat"]);
    }

    [Fact]
    public async Task Run_SessionEndingLater_StaysQuiet()
    {
        using var dbContext = NewContext();
        AddSession(dbContext, Now.AddHours(2), playerAccountId: PlayerId);
        var (runner, sent) = Build(dbContext);

        await runner.RunAsync(CancellationToken.None);

        Assert.Empty(sent.Requests);
    }

    /// Гостю писать некуда: у него нет аккаунта, а значит и телефона в системе.
    [Fact]
    public async Task Run_GuestSession_StaysQuiet()
    {
        using var dbContext = NewContext();
        AddSession(dbContext, Now.AddMinutes(5));
        var (runner, sent) = Build(dbContext);

        await runner.RunAsync(CancellationToken.None);

        Assert.Empty(sent.Requests);
    }

    [Fact]
    public async Task Run_EndedSession_StaysQuiet()
    {
        using var dbContext = NewContext();
        AddSession(dbContext, Now.AddMinutes(5), SessionStateNames.Ended, PlayerId);
        var (runner, sent) = Build(dbContext);

        await runner.RunAsync(CancellationToken.None);

        Assert.Empty(sent.Requests);
    }

    /// Задание тикает чаще окна — ключ идемпотентности не даёт позвонить игроку дважды.
    [Fact]
    public async Task Run_Twice_UsesTheSameIdempotencyKey()
    {
        using var dbContext = NewContext();
        AddSession(dbContext, Now.AddMinutes(5), playerAccountId: PlayerId);
        var (runner, sent) = Build(dbContext);

        await runner.RunAsync(CancellationToken.None);
        await runner.RunAsync(CancellationToken.None);

        Assert.Equal(2, sent.Requests.Count);
        Assert.Equal(sent.Requests[0].IdempotencyKey, sent.Requests[1].IdempotencyKey);
    }

    /// Час до брони — это «пора выходить». Время показываем по часам клуба, а не по UTC.
    [Fact]
    public async Task Run_ReservationWithinAnHour_NotifiesInClubLocalTime()
    {
        using var dbContext = NewContext();
        AddReservation(dbContext, Now.AddMinutes(40));
        var (runner, sent) = Build(dbContext);

        await runner.RunAsync(CancellationToken.None);

        var request = Assert.Single(sent.Requests);
        Assert.Equal(NotificationTemplateKeys.PlayerReservationSoon, request.TemplateKey);
        Assert.Equal("На Рудаки", request.Tokens["club"]);
        // 18:40 UTC — это 23:40 в Душанбе (UTC+5).
        Assert.Equal("23:40", request.Tokens["time"]);
    }

    [Fact]
    public async Task Run_CancelledReservation_StaysQuiet()
    {
        using var dbContext = NewContext();
        AddReservation(dbContext, Now.AddMinutes(40), ReservationStateNames.Cancelled);
        var (runner, sent) = Build(dbContext);

        await runner.RunAsync(CancellationToken.None);

        Assert.Empty(sent.Requests);
    }

    [Fact]
    public async Task Run_ReservationTomorrow_StaysQuiet()
    {
        using var dbContext = NewContext();
        AddReservation(dbContext, Now.AddDays(1));
        var (runner, sent) = Build(dbContext);

        await runner.RunAsync(CancellationToken.None);

        Assert.Empty(sent.Requests);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<NotificationRequest> Requests { get; } = [];

        public Task<NotificationHandle> SendAsync(NotificationRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new NotificationHandle([Guid.NewGuid()], true));
        }

        public Task<NotificationDeliveryResult> SendNowAsync(NotificationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
