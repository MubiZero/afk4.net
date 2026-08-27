using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// Смена ключа игрового ПК без визита к нему.
///
/// До этого сервер умел перевыпустить ключ, а агент — нет: перевыпуск отрезал машину от сети до
/// тех пор, пока человек не поправит файл на ней руками. Теперь клуб просит, агент меняет ключ
/// сам, а старый ещё некоторое время принимается — на случай, если ПК выключится ровно между
/// ответом сервера и записью нового ключа на диск.
/// </summary>
public sealed class AgentCredentialRotationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record SeededDevice(Guid OrganizationId, Guid BranchId, Guid DeviceId, string Secret);

    private static async Task<SeededDevice> SeedDeviceAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var secret = DeviceCredentialSecrets.CreateCredentialSecret();

        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = "club-" + organizationId.ToString("N")[..8],
            Name = "Rotation Club",
            Status = "active",
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Slug = "main",
            Name = "На Рудаки",
            City = "Душанбе",
            CreatedAtUtc = Now
        });
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = deviceId,
            OrganizationId = organizationId,
            BranchId = branchId,
            MachineName = "PC-01",
            DisplayName = "PC-01",
            Role = DeviceRoleNames.GamingPc,
            EnrollmentState = DeviceEnrollmentStateNames.Approved,
            EnrolledAtUtc = Now
        });
        db.DeviceCredentials.Add(new DeviceCredentialEntity
        {
            CredentialId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            DeviceId = deviceId,
            SecretHash = DeviceCredentialSecrets.HashSecret(secret),
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return new SeededDevice(organizationId, branchId, deviceId, secret);
    }

    private static HttpRequestMessage SelfRotate(SeededDevice device, string secret)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Post, $"/api/devices/{device.DeviceId:D}/credentials/self-rotate")
        {
            Content = JsonContent.Create(new SelfRotateDeviceCredentialRequest(
                device.OrganizationId, device.BranchId, device.DeviceId))
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, secret);
        return message;
    }

    private static HttpRequestMessage Heartbeat(SeededDevice device, string secret)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{device.DeviceId:D}/heartbeat")
        {
            Content = JsonContent.Create(new DeviceHeartbeatRequest(
                device.OrganizationId, device.BranchId, device.DeviceId, "PC-01", "0.1.0", "0.1.0",
                DateTimeOffset.UtcNow, false, null, null, null))
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, secret);
        return message;
    }

    private static async Task RequestRotationAsync(PlatformApiFactory factory, Guid deviceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var device = await db.Devices.SingleAsync(candidate => candidate.DeviceId == deviceId);
        device.CredentialRotationRequestedAtUtc = Now;
        await db.SaveChangesAsync();
    }

    // Ради этого всё и делается: машина меняет ключ, предъявив старый, и человек к ней не едет.
    [Fact]
    public async Task Agent_RotatesItsOwnCredential_AndTheNewOneWorks()
    {
        await using var factory = new PlatformApiFactory();
        var device = await SeedDeviceAsync(factory);
        using var client = factory.CreateClient();

        var rotated = await client.SendAsync(SelfRotate(device, device.Secret));

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var response = await rotated.Content.ReadFromJsonAsync<RotateDeviceCredentialResponse>();
        Assert.NotNull(response);
        Assert.NotEqual(device.Secret, response!.CredentialSecret);

        var heartbeat = await client.SendAsync(Heartbeat(device, response.CredentialSecret));
        Assert.Equal(HttpStatusCode.OK, heartbeat.StatusCode);
    }

    /// Между «сервер выдал новый ключ» и «агент записал его на диск» ПК может выключиться. Без
    /// перекрытия такая секунда оставила бы машину без входа — то есть ровно та беда, от которой
    /// эта работа и лечит.
    [Fact]
    public async Task OldCredential_KeepsWorkingRightAfterTheRotation()
    {
        await using var factory = new PlatformApiFactory();
        var device = await SeedDeviceAsync(factory);
        using var client = factory.CreateClient();

        await client.SendAsync(SelfRotate(device, device.Secret));
        var heartbeat = await client.SendAsync(Heartbeat(device, device.Secret));

        Assert.Equal(HttpStatusCode.OK, heartbeat.StatusCode);
    }

    // Перекрытие — запас на минуты, а не вторая жизнь ключа: когда оно кончилось, старый мёртв.
    [Fact]
    public async Task OldCredential_StopsWorkingOnceTheOverlapExpires()
    {
        await using var factory = new PlatformApiFactory();
        var device = await SeedDeviceAsync(factory);
        using var client = factory.CreateClient();
        await client.SendAsync(SelfRotate(device, device.Secret));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            // Двигаем не часы сервера, а срок самого ключа: результат тот же, а прогон не ждёт.
            var expiring = await db.DeviceCredentials
                .Where(candidate => candidate.DeviceId == device.DeviceId && candidate.ExpiresAtUtc != null)
                .ToListAsync();
            foreach (var credential in expiring)
            {
                credential.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            }

            await db.SaveChangesAsync();
        }

        var heartbeat = await client.SendAsync(Heartbeat(device, device.Secret));

        Assert.Equal(HttpStatusCode.Unauthorized, heartbeat.StatusCode);
    }

    [Fact]
    public async Task SelfRotate_WithoutAValidCredential_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        var device = await SeedDeviceAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.SendAsync(SelfRotate(device, "not-the-secret"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await db.DeviceCredentials.Where(c => c.DeviceId == device.DeviceId).ToListAsync());
    }

    // Просьба клуба доезжает до машины сердцебиением — второй трубы для этого не нужно.
    [Fact]
    public async Task Heartbeat_CarriesTheClubsRotationRequest()
    {
        await using var factory = new PlatformApiFactory();
        var device = await SeedDeviceAsync(factory);
        using var client = factory.CreateClient();

        var before = await client.SendAsync(Heartbeat(device, device.Secret));
        var beforeBody = await before.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>();
        Assert.False(beforeBody!.RotateCredential);

        await RequestRotationAsync(factory, device.DeviceId);

        var after = await client.SendAsync(Heartbeat(device, device.Secret));
        var afterBody = await after.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>();
        Assert.True(afterBody!.RotateCredential);
    }

    // Иначе агент менял бы ключ на каждом сердцебиении до скончания века.
    [Fact]
    public async Task Rotation_StopsBeingAskedOnceItHappened()
    {
        await using var factory = new PlatformApiFactory();
        var device = await SeedDeviceAsync(factory);
        using var client = factory.CreateClient();
        await RequestRotationAsync(factory, device.DeviceId);

        var rotated = await client.SendAsync(SelfRotate(device, device.Secret));
        var response = await rotated.Content.ReadFromJsonAsync<RotateDeviceCredentialResponse>();

        var heartbeat = await client.SendAsync(Heartbeat(device, response!.CredentialSecret));
        var body = await heartbeat.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>();
        Assert.False(body!.RotateCredential);
    }

    /// Жёсткий перевыпуск остаётся жёстким: у украденного ПК ключ обязан умереть сразу, без
    /// всяких перекрытий.
    [Fact]
    public async Task ManualRotation_StillCutsTheMachineOffImmediately()
    {
        await using var factory = new PlatformApiFactory();
        var device = await SeedDeviceAsync(factory);
        using var client = factory.CreateClient();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var lifecycle = scope.ServiceProvider
                .GetRequiredService<IDeviceCredentialLifecycleService>();
            await lifecycle.RotateAsync(device.DeviceId, CancellationToken.None);
        }

        var heartbeat = await client.SendAsync(Heartbeat(device, device.Secret));

        Assert.Equal(HttpStatusCode.Unauthorized, heartbeat.StatusCode);
    }
}
