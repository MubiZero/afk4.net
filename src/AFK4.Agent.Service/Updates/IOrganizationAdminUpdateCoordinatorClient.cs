namespace AFK4.Agent.Service.Updates;

public interface IOrganizationAdminUpdateCoordinatorClient
{
    Task<OrganizationAdminCoordinationResult> QueryStateAsync(CancellationToken cancellationToken);

    Task<OrganizationAdminCoordinationResult> RequestShutdownAsync(
        Guid updateRolloutId,
        Guid updatePackageId,
        CancellationToken cancellationToken);
}

public sealed record OrganizationAdminCoordinationResult(string Status, string Message);
