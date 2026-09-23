namespace AFK4.Shared.Contracts.Identity;

/// <summary>
/// Сотрудник организации, которого нет в этом филиале: его можно добавить сюда ролями. Филиалы, где
/// он уже работает, — названиями; пустой список значит, что назначений у него не осталось вовсе и
/// войти в Панель ему некуда, пока его не вернут в филиал.
/// </summary>
public sealed record StaffBranchCandidateDto(
    Guid StaffUserId,
    string UserName,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<string> BranchNames);
