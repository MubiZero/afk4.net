namespace AFK4.OrganizationAdmin.App.Auth;

public interface IOrganizationAdminTokenStore
{
    Task SaveAsync(OrganizationAdminTokenSnapshot snapshot, CancellationToken cancellationToken);

    Task<OrganizationAdminTokenSnapshot?> LoadAsync(CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
