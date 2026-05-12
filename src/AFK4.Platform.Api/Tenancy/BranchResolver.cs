using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tenancy;

public sealed class BranchResolver(PlatformDbContext dbContext) : IBranchResolver
{
    public Task<BranchEntity?> FindAsync(Guid branchId, CancellationToken cancellationToken)
    {
        return dbContext.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(branch => branch.BranchId == branchId, cancellationToken);
    }
}
