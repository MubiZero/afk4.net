namespace AFK4.Platform.Api.Platform.Identity;

/// <summary>
/// Resolves what a set of platform role names currently grants, straight from the database, on
/// every call. There is deliberately no cache here: a permission removed from a role in the panel
/// must stop working on the very next request, not after the admin's session ends.
/// </summary>
public interface IPlatformRolePermissionResolver
{
    Task<IReadOnlySet<string>> ResolveAsync(IEnumerable<string> roleNames, CancellationToken cancellationToken);

    Task<bool> IsKnownRoleAsync(string roleName, CancellationToken cancellationToken);
}
