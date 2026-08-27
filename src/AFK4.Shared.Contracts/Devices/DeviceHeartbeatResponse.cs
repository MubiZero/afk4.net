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
    bool RotateCredential = false);
