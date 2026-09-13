using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Shared.Contracts.Install;

public sealed record InstallDiscoverResponse(
    string OwnerDisplayName,
    IReadOnlyList<InstallBranchDto> Branches,
    /// Оформление клуба уже задано. Мастер спрашивает про логотип и цвет только когда их нет:
    /// админских ПК в клубе бывает несколько, и на втором это был бы не вопрос, а шанс затереть
    /// настроенное. В конце списка и с умолчанием — старый мастер продолжит работать.
    bool BrandingConfigured = false);

public sealed record InstallBranchDto(
    Guid BranchId,
    string Slug,
    string Name,
    FloorMapDto FloorMap,
    IReadOnlyList<Guid> FreeSeatIds,
    /// В зале есть хотя бы один действующий тариф — значит платную сессию начать есть чем.
    bool HasTariff = false,
    /// В зале есть кто-то кроме владельца: приглашать первого сотрудника уже не нужно.
    bool HasStaffBesidesOwner = false);
