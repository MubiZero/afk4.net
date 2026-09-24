using System.Text.Json;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Shared.Contracts.Tests;

public sealed class PlayerShellContractSerializationTests
{
    [Fact]
    public void LockedState_RoundTripsWithoutSession()
    {
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: PlayerShellStateNames.Locked,
            SessionId: null,
            LeaseExpiresAtUtc: null,
            RemainingSeconds: null,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "This PC is locked.",
            LauncherApps: []);

        var json = JsonSerializer.Serialize(state);
        var copy = JsonSerializer.Deserialize<PlayerShellStateDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(PlayerShellStateNames.Locked, copy.State);
        Assert.Null(copy.SessionId);
        Assert.Empty(copy.LauncherApps);
    }

    [Fact]
    public void State_CarriesBranchLocale_DefaultingToRu()
    {
        var defaulted = new PlayerShellStateDto(
            OrganizationId: Guid.Empty,
            BranchId: Guid.Empty,
            DeviceId: Guid.Empty,
            State: PlayerShellStateNames.Locked,
            SessionId: null,
            LeaseExpiresAtUtc: null,
            RemainingSeconds: null,
            IsOnline: false,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "This PC is locked.",
            LauncherApps: []);

        Assert.Equal("ru", defaulted.Locale);

        var tg = defaulted with { Locale = "tg" };
        var copy = JsonSerializer.Deserialize<PlayerShellStateDto>(JsonSerializer.Serialize(tg));

        Assert.NotNull(copy);
        Assert.Equal("tg", copy.Locale);
    }

    [Fact]
    public void ActiveState_RoundTripsSessionCountdownAndLauncherApps()
    {
        var app = new LauncherAppDto(
            AppId: "counter-strike-2",
            DisplayName: "Counter-Strike 2",
            Category: "Games",
            IconUri: "afk4://icons/cs2",
            IsAvailable: true);
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: PlayerShellStateNames.Active,
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            LeaseExpiresAtUtc: DateTimeOffset.Parse("2026-05-14T10:15:00Z"),
            RemainingSeconds: 1800,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "Session is active.",
            LauncherApps: [app]);

        var json = JsonSerializer.Serialize(state);
        var copy = JsonSerializer.Deserialize<PlayerShellStateDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(PlayerShellStateNames.Active, copy.State);
        Assert.Equal(state.SessionId, copy.SessionId);
        Assert.Equal(1800, copy.RemainingSeconds);
        Assert.Single(copy.LauncherApps);
        Assert.Equal("counter-strike-2", copy.LauncherApps[0].AppId);
    }

    [Fact]
    public void State_DefaultsWarningKindToNone_AndBrandingToNull()
    {
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.Empty,
            BranchId: Guid.Empty,
            DeviceId: Guid.Empty,
            State: PlayerShellStateNames.Locked,
            SessionId: null,
            LeaseExpiresAtUtc: null,
            RemainingSeconds: null,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "This PC is locked.",
            LauncherApps: []);

        Assert.Equal(PlayerShellWarningKinds.None, state.WarningKind);
        Assert.Null(state.Branding);
    }

    [Fact]
    public void State_RoundTripsWarningKindAndBranding()
    {
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.Empty,
            BranchId: Guid.Empty,
            DeviceId: Guid.Empty,
            State: PlayerShellStateNames.Active,
            SessionId: null,
            LeaseExpiresAtUtc: null,
            RemainingSeconds: 120,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "Session is active.",
            LauncherApps: [],
            Locale: "ru",
            WarningKind: PlayerShellWarningKinds.LowTime,
            Branding: new ShellBrandingDto("Club AFK4", "https://cdn/x.png", "#c8ff00"));

        var copy = JsonSerializer.Deserialize<PlayerShellStateDto>(JsonSerializer.Serialize(state));

        Assert.NotNull(copy);
        Assert.Equal(PlayerShellWarningKinds.LowTime, copy.WarningKind);
        Assert.NotNull(copy.Branding);
        Assert.Equal("Club AFK4", copy.Branding!.ClubName);
        Assert.Equal("#c8ff00", copy.Branding.AccentColor);
    }

    // Новые поля v2 читают хост и веб-слой по их JSON-именам: переименование в C# без
    // перегенерации контрактов молча оставило бы экран без отсчёта кода и поправки часов.
    [Fact]
    public void StateV2_NewFields_TravelUnderTheirWebNames()
    {
        var observedAt = DateTimeOffset.Parse("2026-09-24T20:00:00Z");
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.Empty,
            BranchId: Guid.Empty,
            DeviceId: Guid.Empty,
            State: PlayerShellStateNames.Locked,
            SessionId: null,
            LeaseExpiresAtUtc: null,
            RemainingSeconds: null,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "This PC is locked.",
            LauncherApps: [],
            SeatingCode: "418207",
            SeatingCodeExpiresAtUtc: observedAt.AddMinutes(1),
            ObservedAtUtc: observedAt,
            LastContactUtc: observedAt.AddSeconds(-4),
            ApiBaseUrl: "https://api.afk4.net/");

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(observedAt.AddMinutes(1), root.GetProperty("seatingCodeExpiresAtUtc").GetDateTimeOffset());
        Assert.Equal(observedAt, root.GetProperty("observedAtUtc").GetDateTimeOffset());
        Assert.Equal(observedAt.AddSeconds(-4), root.GetProperty("lastContactUtc").GetDateTimeOffset());
        Assert.Equal("https://api.afk4.net/", root.GetProperty("apiBaseUrl").GetString());
    }
}
