namespace AFK4.Shared.Contracts.News;

/// <summary>
/// Где сотрудник может публиковать новости: филиалы, где у него есть право на новости, и можно ли
/// писать на всю сеть. На всю сеть — только тому, у кого право во всех филиалах (владелец):
/// управляющий одного филиала не говорит от имени всех.
/// </summary>
public sealed record NewsScopeDto(IReadOnlyList<OwnerBranchSummaryDto> Branches, bool CanPublishToAllBranches);
