namespace AFK4.Operator.App.Auth;

internal static class OrganizationApiRoute
{
    public static string Build(OperatorTokenSnapshot snapshot, string relativePath)
    {
        return $"/api/organizations/{snapshot.OrganizationId:D}/{relativePath.TrimStart('/')}";
    }
}
