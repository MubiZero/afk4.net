using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Tests.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Tests.Sessions;

/// <summary>
/// Ключи ПК администраторов для офлайн-аренд (docs/roadmap/offline-front-desk.md): Панель регистрирует
/// ключ своего ПК, сервер раздаёт действующие ключи агентам филиала, управляющий отзывает.
/// </summary>
public sealed class OfflineLeaseKeyTests
{
    private static readonly string KeysRoute =
        $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/offline-lease-keys";

    [Fact]
    public async Task ARegisteredKey_ReachesTheBranchPcs_AndARevokedOneLeavesThem()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var enrollment = await TestDeviceEnrollment.EnrollDeviceAsync(client, TestIds.OrganizationId, TestIds.BranchId);
        var pem = NewPublicKeyPem();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(KeysRoute, new RegisterOfflineLeaseKeyRequest("desk-1", pem))).StatusCode);
        // Повтор той же регистрации после обрыва связи — не ошибка.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(KeysRoute, new RegisterOfflineLeaseKeyRequest("desk-1", pem))).StatusCode);

        var keys = (await HeartbeatAsync(client, enrollment))!.OfflineLeaseKeys;
        Assert.Equal("desk-1", Assert.Single(keys!).KeyId);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{KeysRoute}/desk-1")).StatusCode);
        Assert.Empty((await HeartbeatAsync(client, enrollment))!.OfflineLeaseKeys!);
        // Отозванное имя заново не занять: Панель создаёт новый ключ.
        var again = await client.PostAsJsonAsync(KeysRoute, new RegisterOfflineLeaseKeyRequest("desk-1", pem));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains(OfflineLeaseKeyErrorCodeNames.Revoked, await again.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnotherKeyUnderTheSameName_OrNotAKey_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await client.PostAsJsonAsync(KeysRoute, new RegisterOfflineLeaseKeyRequest("desk-1", NewPublicKeyPem()));

        var conflict = await client.PostAsJsonAsync(KeysRoute, new RegisterOfflineLeaseKeyRequest("desk-1", NewPublicKeyPem()));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains(OfflineLeaseKeyErrorCodeNames.Conflict, await conflict.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var rsa = RSA.Create(2048);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(KeysRoute, new RegisterOfflineLeaseKeyRequest("desk-2", rsa.ExportSubjectPublicKeyInfoPem()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(KeysRoute, new RegisterOfflineLeaseKeyRequest("desk 3!", NewPublicKeyPem()))).StatusCode);
    }

    // Регистрирует ключ тот, кто сажает гостей; техник гостей не сажает.
    [Fact]
    public async Task SomeoneWhoCannotStartSessions_CannotRegisterAKey()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(KeysRoute, new RegisterOfflineLeaseKeyRequest("desk-1", NewPublicKeyPem()))).StatusCode);
    }

    private static string NewPublicKeyPem()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return key.ExportSubjectPublicKeyInfoPem();
    }

    private static async Task<DeviceHeartbeatResponse?> HeartbeatAsync(HttpClient client, InstallEnrollResponse enrollment)
    {
        var request = new DeviceHeartbeatRequest(
            TestIds.OrganizationId, TestIds.BranchId, enrollment.DeviceId, "PC-001", "0.1.0", "0.1.0",
            DateTimeOffset.UtcNow, IsLocked: true, ActiveSessionId: null, ActiveSessionLeaseExpiresAtUtc: null, ActiveSessionLeaseSequence: null);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{enrollment.DeviceId}/heartbeat")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        using var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>();
    }
}
