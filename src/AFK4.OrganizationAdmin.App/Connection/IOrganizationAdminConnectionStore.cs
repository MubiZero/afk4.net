namespace AFK4.OrganizationAdmin.App.Connection;

public interface IOrganizationAdminConnectionStore
{
    Task SaveAsync(OrganizationAdminConnectionSnapshot snapshot, CancellationToken cancellationToken);

    Task<OrganizationAdminConnectionSnapshot?> LoadAsync(CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
