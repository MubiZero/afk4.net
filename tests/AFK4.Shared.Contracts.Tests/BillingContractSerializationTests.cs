using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Shared.Contracts.Tests;

public sealed class BillingContractSerializationTests
{
    [Fact]
    public void WalletSummary_RoundTripsDerivedBalancesAndRecentEntries()
    {
        var entry = new LedgerEntryDto(
            LedgerEntryId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            PlayerAccountId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            SessionId: null,
            PlayerPackageId: null,
            EntryType: LedgerEntryTypeNames.TopUp,
            AccountType: LedgerAccountTypeNames.Wallet,
            Amount: new MoneyDto("TJS", 5000),
            QuantitySeconds: 0,
            Description: "Cash top-up",
            Reason: "front-desk top-up",
            ReversesLedgerEntryId: null,
            CreatedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"));
        var summary = new WalletSummaryDto(
            PlayerAccountId: entry.PlayerAccountId,
            WalletBalance: new MoneyDto("TJS", 3500),
            HeldBalance: new MoneyDto("TJS", 1500),
            DebtBalance: new MoneyDto("TJS", 0),
            RecentEntries: [entry]);

        var json = JsonSerializer.Serialize(summary);
        var copy = JsonSerializer.Deserialize<WalletSummaryDto>(json);

        Assert.NotNull(copy);
        // Остаток — это по-прежнему «сколько можно потратить»: придержанное из него уже вычтено,
        // и третье число ничего не переносит, оно только объясняет разницу.
        Assert.Equal(3500, copy.WalletBalance.MinorUnits);
        Assert.Equal(1500, copy.HeldBalance.MinorUnits);
        Assert.Equal(0, copy.DebtBalance.MinorUnits);
        Assert.Single(copy.RecentEntries);
        Assert.Equal(LedgerEntryTypeNames.TopUp, copy.RecentEntries[0].EntryType);
    }

    [Fact]
    public void PlayerDashboard_CarriesHeldMoneyAlongsideWalletAndDebt()
    {
        var dashboard = new PlayerDashboardDto(
            WalletBalance: new MoneyDto("TJS", 3500),
            HeldBalance: new MoneyDto("TJS", 1500),
            DebtBalance: new MoneyDto("TJS", 700),
            ActiveSession: null);

        var copy = JsonSerializer.Deserialize<PlayerDashboardDto>(JsonSerializer.Serialize(dashboard));

        Assert.NotNull(copy);
        Assert.Equal(3500, copy.WalletBalance.MinorUnits);
        Assert.Equal(1500, copy.HeldBalance.MinorUnits);
        Assert.Equal(700, copy.DebtBalance.MinorUnits);
    }

    /// <summary>
    /// Строки типов записей уезжают в базу и в выписку игрока, поэтому меняться они не вправе.
    /// <c>reservation_hold</c> писался и раньше — из <c>Reservations/ReservationHold.cs</c>; здесь
    /// он получает общий дом, а не новое значение.
    /// </summary>
    [Fact]
    public void LedgerEntryTypeNames_ExposeReservationMoney()
    {
        Assert.Equal("reservation_hold", LedgerEntryTypeNames.ReservationHold);
        Assert.Equal("reservation_no_show_fee", LedgerEntryTypeNames.ReservationNoShowFee);
    }

    [Fact]
    public void StartGuestSessionRequest_LegacyJsonDefaultsBillingFields()
    {
        const string json = """
            {
              "OrganizationId": "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
              "SeatId": "11111111-1111-4111-8111-111111111111",
              "DurationMinutes": 60,
              "TariffRuleVersionId": "legacy-tariff-rule",
              "IdempotencyKey": "legacy-start-001"
            }
            """;

        var copy = JsonSerializer.Deserialize<StartGuestSessionRequest>(json);

        Assert.NotNull(copy);
        Assert.Null(copy.PlayerAccountId);
        Assert.Equal("", copy.BillingMode);
        Assert.Null(copy.TariffVersionId);
        Assert.Null(copy.PlayerPackageId);
    }

    [Fact]
    public void StartGuestSessionRequest_RoundTripsBillingFieldsThroughJson()
    {
        var playerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
        var tariffVersionId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");
        var playerPackageId = Guid.Parse("dddddddd-dddd-4ddd-dddd-dddddddddddd");
        var start = new StartGuestSessionRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60,
            TariffRuleVersionId: tariffVersionId.ToString("D"),
            IdempotencyKey: "start-prepaid-001",
            PlayerAccountId: playerAccountId,
            BillingMode: BillingModeNames.PrepaidWallet,
            TariffVersionId: tariffVersionId,
            PlayerPackageId: playerPackageId);

        var json = JsonSerializer.Serialize(start);
        var copy = JsonSerializer.Deserialize<StartGuestSessionRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(playerAccountId, copy.PlayerAccountId);
        Assert.Equal(BillingModeNames.PrepaidWallet, copy.BillingMode);
        Assert.Equal(tariffVersionId, copy.TariffVersionId);
        Assert.Equal(playerPackageId, copy.PlayerPackageId);
    }

    [Fact]
    public void ExtendSessionRequest_LegacyJsonDefaultsBillingFields()
    {
        const string json = """
            {
              "AdditionalMinutes": 30,
              "TariffRuleVersionId": "legacy-tariff-rule",
              "IdempotencyKey": "legacy-extend-001"
            }
            """;

        var copy = JsonSerializer.Deserialize<ExtendSessionRequest>(json);

        Assert.NotNull(copy);
        Assert.Null(copy.PlayerAccountId);
        Assert.Equal("", copy.BillingMode);
        Assert.Null(copy.TariffVersionId);
        Assert.Null(copy.PlayerPackageId);
    }

    [Fact]
    public void ExtendSessionRequest_RoundTripsBillingFieldsThroughJson()
    {
        var playerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
        var tariffVersionId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");
        var playerPackageId = Guid.Parse("dddddddd-dddd-4ddd-dddd-dddddddddddd");
        var request = new ExtendSessionRequest(
            AdditionalMinutes: 30,
            TariffRuleVersionId: tariffVersionId.ToString("D"),
            IdempotencyKey: "extend-package-001",
            PlayerAccountId: playerAccountId,
            BillingMode: BillingModeNames.Package,
            TariffVersionId: tariffVersionId,
            PlayerPackageId: playerPackageId);

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<ExtendSessionRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(playerAccountId, copy.PlayerAccountId);
        Assert.Equal(BillingModeNames.Package, copy.BillingMode);
        Assert.Equal(tariffVersionId, copy.TariffVersionId);
        Assert.Equal(playerPackageId, copy.PlayerPackageId);
    }

    /// <summary>
    /// Карточка клиента называет личность за ней и то, кто её завёл. Идентификатор личности —
    /// то, чем оператор спрашивает сеть про знакомого человека, не диктуя его телефон в аудит;
    /// пометка «из приложения» объясняет стойке карточку, которую она не заводила.
    /// </summary>
    [Fact]
    public void PlayerAccountDto_RoundTripsThePersonBehindTheCardAndItsOrigin()
    {
        var fromApp = new PlayerAccountDto(
            PlayerAccountId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            HomeBranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DisplayName: "Фаррух",
            PhoneNumber: "+992900000001",
            IsActive: true,
            CreatedAtUtc: DateTimeOffset.Parse("2026-08-21T09:00:00Z"),
            PlatformPersonId: Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
            CreatedFromApp: true);
        var deskCard = fromApp with { PlatformPersonId = null, CreatedFromApp = false };

        var fromAppCopy = JsonSerializer.Deserialize<PlayerAccountDto>(JsonSerializer.Serialize(fromApp));
        var deskCardCopy = JsonSerializer.Deserialize<PlayerAccountDto>(JsonSerializer.Serialize(deskCard));

        Assert.NotNull(fromAppCopy);
        Assert.Equal(fromApp.PlatformPersonId, fromAppCopy.PlatformPersonId);
        Assert.True(fromAppCopy.CreatedFromApp);
        Assert.NotNull(deskCardCopy);
        Assert.Null(deskCardCopy.PlatformPersonId);
        Assert.False(deskCardCopy.CreatedFromApp);
    }
}
