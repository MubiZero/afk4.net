namespace AFK4.Platform.Api.Platform.Support;

/// <summary>
/// Что поддержка может менять под грантом. Список отдаётся админке клиента, чтобы она гасила
/// недоступное заранее: кнопка, которая всегда возвращает 403, читается как поломка продукта.
/// </summary>
public static class PlatformSupportWritableAreas
{
    public const string BranchSettings = "branch-settings";
    public const string Devices = "devices";
    public const string Staff = "staff";
    public const string FloorMap = "floor-map";
    public const string BranchProfile = "branch-profile";

    public static readonly IReadOnlyList<string> All =
        [BranchSettings, Devices, Staff, FloorMap, BranchProfile];
}
