using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.News;

/// <summary>
/// Какие новости сотруднику можно видеть и править. Роли выдаются по филиалам, и право на новости
/// «хоть где-то» не даёт управляющему одного филиала переписывать новости соседнего и всей сети.
/// </summary>
public sealed record NewsScope(IReadOnlyDictionary<Guid, string> Branches, bool OrganizationWide)
{
    /// <summary>null — новость на всю сеть.</summary>
    public bool Allows(Guid? branchId) => branchId is { } id ? Branches.ContainsKey(id) : OrganizationWide;

    public static async Task<NewsScope> ForAsync(StaffContext staff, PlatformDbContext db, CancellationToken ct)
    {
        var all = await db.Branches.AsNoTracking()
            .Where(branch => branch.OrganizationId == staff.OrganizationId)
            .OrderBy(branch => branch.Name)
            .Select(branch => new { branch.BranchId, branch.Name })
            .ToListAsync(ct);
        var manageable = all
            .Where(branch => staff.HasBranchPermission(branch.BranchId, OrganizationPermissionNames.ManageNews))
            .ToDictionary(branch => branch.BranchId, branch => branch.Name);
        return new NewsScope(manageable, all.Count > 0 && manageable.Count == all.Count);
    }
}
