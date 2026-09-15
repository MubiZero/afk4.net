namespace AFK4.Shared.Contracts.Branches;

public sealed record BranchSettingsDto(
    Guid OrganizationId,
    Guid BranchId,
    bool RequireManualDeviceApproval,
    string PreferredLocale,
    // Допустимое расхождение кассы при закрытии смены, в минорных единицах. Больше него смену
    // закрывает только подпись второго менеджера (анти-фрод §5.7), и стойка должна знать порог
    // заранее: спросить подпись до отправки честнее, чем отказать после.
    //
    // Отдаётся уже разрешённым — с подставленным умолчанием, если у филиала своего нет.
    long ShiftDiscrepancyToleranceMinorUnits = 0);
