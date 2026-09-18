using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

/// Ожидание ответа на заявку — самое тревожное место продукта: деньги заморожены с момента
/// отправки, а решение клуба до этого приходило только в список, который надо было открыть и
/// потянуть вниз.
public sealed class PlayerReservationAnswerNotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid OrgId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid BranchId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid PlayerId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");

    private static PlatformDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"reservation-answers-{Guid.NewGuid()}")
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
        dbContext.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerId,
            OrganizationId = OrgId,
            HomeBranchId = BranchId,
            DisplayName = "Иван",
            PhoneNumber = "+992900000001",
            PreferredLocale = "ru",
            IsActive = true,
            CreatedAtUtc = Now,
        });
        dbContext.SaveChanges();
        return dbContext;
    }

    private static (PlayerPushNotifier Notifier, RecordingNotifications Sent) Build(PlatformDbContext dbContext)
    {
        var sent = new RecordingNotifications();
        return (new PlayerPushNotifier(dbContext, sent, NullLogger<PlayerPushNotifier>.Instance), sent);
    }

    [Fact]
    public async Task Confirmed_TellsThePlayerInClubLocalTime()
    {
        using var dbContext = NewContext();
        var (notifier, sent) = Build(dbContext);

        await notifier.ReservationAnsweredAsync(
            PlayerId, OrgId, BranchId, Now.AddHours(5), confirmed: true, rejectReasonNote: null,
            CancellationToken.None);

        var request = Assert.Single(sent.Requests);
        Assert.Equal(NotificationTemplateKeys.PlayerReservationConfirmed, request.TemplateKey);
        Assert.Equal("На Рудаки", request.Tokens["club"]);
        // 19:00 UTC — это полночь в Душанбе (UTC+5).
        Assert.Equal("00:00", request.Tokens["time"]);
    }

    /// Причина — словами администратора: «деньги вернулись, а почему — неизвестно» хуже пустого
    /// места.
    [Fact]
    public async Task Rejected_CarriesTheReasonTheStaffWrote()
    {
        using var dbContext = NewContext();
        var (notifier, sent) = Build(dbContext);

        await notifier.ReservationAnsweredAsync(
            PlayerId, OrgId, BranchId, Now.AddHours(5), confirmed: false,
            rejectReasonNote: "  Зал закрыт на турнир  ", CancellationToken.None);

        var request = Assert.Single(sent.Requests);
        Assert.Equal(NotificationTemplateKeys.PlayerReservationRejected, request.TemplateKey);
        Assert.Equal("Зал закрыт на турнир", request.Tokens["reason"]);
    }

    /// Администратор причину не написал — уведомление всё равно уходит: главное в нём то, что
    /// вечер не состоится и деньги вернулись.
    [Fact]
    public async Task Rejected_WithoutANote_StillTellsThePlayer()
    {
        using var dbContext = NewContext();
        var (notifier, sent) = Build(dbContext);

        await notifier.ReservationAnsweredAsync(
            PlayerId, OrgId, BranchId, Now.AddHours(5), confirmed: false, rejectReasonNote: null,
            CancellationToken.None);

        var request = Assert.Single(sent.Requests);
        Assert.Equal(string.Empty, request.Tokens["reason"]);
    }

    /// Подтверждение и отказ — разные события: их ключи не должны совпасть, иначе второй ответ
    /// клуба (заявку подтвердили, потом отказали) не дошёл бы вовсе.
    [Fact]
    public async Task ConfirmedAndRejected_DoNotShareAnIdempotencyKey()
    {
        using var dbContext = NewContext();
        var (notifier, sent) = Build(dbContext);
        var startsAt = Now.AddHours(5);

        await notifier.ReservationAnsweredAsync(
            PlayerId, OrgId, BranchId, startsAt, confirmed: true, rejectReasonNote: null,
            CancellationToken.None);
        await notifier.ReservationAnsweredAsync(
            PlayerId, OrgId, BranchId, startsAt, confirmed: false, rejectReasonNote: null,
            CancellationToken.None);

        Assert.Equal(2, sent.Requests.Count);
        Assert.NotEqual(sent.Requests[0].IdempotencyKey, sent.Requests[1].IdempotencyKey);
    }

    private sealed class RecordingNotifications : INotificationService
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
