using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Support;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformSupportSessionMiddlewareTests
{
    [Fact]
    public async Task UnmarkedEndpoint_WithValidSession_IsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId) = await SupportAccessTestHelper.OpenSessionAsync(factory);

        client.DefaultRequestHeaders.Add(
            PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        // Смены денежные и никогда не помечаются для поддержки.
        var response = await client.GetAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/shifts/current");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MarkedReadEndpoint_WithValidSession_Succeeds()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId) = await SupportAccessTestHelper.OpenSessionAsync(factory);

        client.DefaultRequestHeaders.Add(
            PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        var response = await client.GetAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MarkedEndpoint_WithUnknownSession_IsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, organizationId, branchId) = await SupportAccessTestHelper.OpenSessionAsync(factory);

        client.DefaultRequestHeaders.Add(
            PlatformSupportAccessGrantService.GrantHeaderName, "не-существующий-токен");

        var response = await client.GetAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A session token is scoped to the organization its grant was issued for. If someone reuses it
    // against a different organization's route, AuthenticationDomainEnforcementMiddleware must catch
    // the mismatch between the synthetic StaffContext's OrganizationId (the grant's org) and the
    // route's {organizationId} the same way it does for a real staff session.
    [Fact]
    public async Task MarkedReadEndpoint_WithSessionForAnotherOrganization_IsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, _, branchId) = await SupportAccessTestHelper.OpenSessionAsync(factory);
        var (_, foreignOrganizationId, _) = await SupportAccessTestHelper.OpenSessionAsync(factory);

        client.DefaultRequestHeaders.Add(
            PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        var response = await client.GetAsync(
            $"/api/organizations/{foreignOrganizationId}/branches/{branchId}/settings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
