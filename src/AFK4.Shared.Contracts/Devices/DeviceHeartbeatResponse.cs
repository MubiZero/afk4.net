using AFK4.Shared.Contracts.Shell;

namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceHeartbeatResponse(
    DateTimeOffset ServerTimeUtc,
    int HeartbeatIntervalSeconds,
    IReadOnlyList<DeviceCommandDto> Commands,
    // Effective offline grace window (minutes) for this device's branch. The agent applies it locally
    // to keep a paying customer playing for this long after the network actually drops (spec §6.1).
    int EffectiveGraceMinutes = 15,
    // Код, который простаивающий ПК показывает на мониторе, чтобы человек мог сесть за него из
    // приложения. Едет здесь, а не своим маршрутом: сердцебиение и так стучит раз в десять
    // секунд, а код живёт минуты — вторая труба к тому же серверу за тем же самым ничего бы не
    // добавила, кроме второго места, где это можно сломать.
    //
    // null, когда за ПК уже играют: звать к занятой машине незачем.
    string? SeatingCode = null,
    DateTimeOffset? SeatingCodeExpiresAtUtc = null,
    /// Клуб попросил сменить ключ этой машины. Агент меняет его сам и записывает новый — без
    /// визита к ПК и без простоя. Едет сердцебиением по той же причине, что и код посадки:
    /// вторая труба к тому же серверу за тем же самым ничего не добавила бы.
    bool RotateCredential = false,
    /// Оформление клуба для экрана игрока: название, логотип, цвет. Едет сердцебиением по той же
    /// причине, что код посадки и ротация ключа. Хранить его в конфиге машины было бы хуже: клуб
    /// меняет логотип в панели, а не обходом всех ПК с переустановкой.
    ///
    /// null, когда оформление не задано, — оболочка показывает нейтральный экран.
    ShellBrandingDto? Branding = null,
    /// Место этого ПК: оболочка пишет его в шапке. null — ПК ни к какому месту не привязан.
    DeviceSeatDto? Seat = null,
    /// Чья сессия идёт на ПК: вошедшему не владельцу оболочка чужую сессию не откроет.
    DeviceSessionOwnerDto? SessionOwner = null,
    /// Права организации по тарифу (PlatformFeatureNames): оболочка прячет разделы, которых у клуба
    /// нет, — бар без player_shop, кэшбек без loyalty. Тот же расчёт, что у /api/me/features.
    IReadOnlyList<string>? Features = null,
    /// Заявка на вход с телефона, которую ПК ещё не забрал, — на случай, если сигнал SignalR
    /// потерялся. null — ждать нечего.
    PlayerSignInClaimedDto? PendingSignInClaim = null,
    /// ПК на обслуживании. Команду maintenance-on агент получает сразу, а по этому признаку
    /// догоняет, если её пропустил, и выходит из обслуживания, если пропустил maintenance-off.
    bool Maintenance = false);
