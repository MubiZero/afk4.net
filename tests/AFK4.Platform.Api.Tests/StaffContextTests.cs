using AFK4.Platform.Api.Identity;

namespace AFK4.Platform.Api.Tests;

public class StaffContextTests
{
    private static StaffContext Ctx(Dictionary<Guid, IReadOnlySet<string>> byBranch)
    {
        var branchIds = byBranch.Keys.ToHashSet();
        var union = byBranch.Values.SelectMany(p => p).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new StaffContext(Guid.NewGuid(), Guid.NewGuid(), "Test",
            branchIds, union) { PermissionsByBranch = byBranch };
    }

    [Fact]
    public void HasBranchPermission_true_only_for_branch_that_grants_it()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var ctx = Ctx(new()
        {
            [a] = new HashSet<string> { "branches.settings.manage" },
            [b] = new HashSet<string> { "pos.sell" }
        });

        Assert.True(ctx.HasBranchPermission(a, "branches.settings.manage"));
        Assert.False(ctx.HasBranchPermission(b, "branches.settings.manage")); // не протекает на B
        Assert.True(ctx.HasBranchPermission(b, "POS.SELL"));                    // case-insensitive
        Assert.False(ctx.HasBranchPermission(Guid.NewGuid(), "pos.sell"));      // неизвестный branch
    }
}
