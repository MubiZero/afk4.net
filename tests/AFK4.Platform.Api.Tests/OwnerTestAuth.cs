using AFK4.Platform.Api.Identity;

namespace AFK4.Platform.Api.Tests;

// Thin adapter over StaffAuthTestHelper for the owner-cabinet payment tests.
// Seeds a staff member with the given role and signs the passed HttpClient in
// (sets the Bearer token). Returns the seeded organization id where useful.
internal static class OwnerTestAuth
{
    public static async Task<(Guid OrganizationId, HttpClient Client)> SignInOwnerAsync(
        PlatformApiFactory factory, HttpClient client)
    {
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        return (TestIds.OrganizationId, client);
    }

    // Signs in a staff member whose role lacks payments.gateways.manage.
    public static async Task<HttpClient> SignInNonOwnerAsync(
        PlatformApiFactory factory, HttpClient client)
    {
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        return client;
    }
}
