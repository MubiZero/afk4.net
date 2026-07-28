namespace AFK4.OrganizationAdmin.App.Auth;

internal static class OrganizationApiRoute
{
    public static string Build(OrganizationAdminTokenSnapshot snapshot, string relativePath)
    {
        return $"/api/organizations/{snapshot.OrganizationId:D}/{relativePath.TrimStart('/')}";
    }
}
