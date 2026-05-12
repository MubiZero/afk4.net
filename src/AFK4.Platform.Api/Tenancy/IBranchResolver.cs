using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Tenancy;

public interface IBranchResolver
{
    Task<BranchEntity?> FindAsync(Guid branchId, CancellationToken cancellationToken);
}
