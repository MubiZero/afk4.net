using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// Ставит ПК тем же путём, каким его ставит техник в жизни: код установки в Панели
/// (<see cref="InstallCodeRoutes.Branch"/>), потом сам код на машине (<see cref="InstallRoutes.CodeEnroll"/>).
/// Старый одноразовый код филиала (`device-enrollment-codes` + `/api/devices/enroll`) убран —
/// живым путём пользуются и техник, и тесты.
/// </summary>
internal static class TestDeviceEnrollment
{
    private static readonly Guid InstallerStaffUserId = Guid.Parse("7a3c0e6e-2f0c-4f77-9d3c-9f6f9c6c7a10");
    private const string InstallerUserName = "installer@afk4.test";
    private const string InstallerPassword = "246813";

    /// <summary>Клиент уже авторизован сотрудником с правом ставить ПК (Owner/BranchManager/Technician).</summary>
    public static Task<InstallEnrollResponse> EnrollDeviceAsync(
        HttpClient client,
        Guid organizationId,
        Guid branchId,
        string machineName = "PC-001",
        string? seatName = null,
        string? displayName = null,
        string? publicKey = null,
        int maxDevices = 100) =>
        EnrollCoreAsync(client, client, organizationId, branchId, machineName, seatName, displayName, publicKey, maxDevices);

    /// <summary>
    /// Основной клиент теста правом ставить ПК не обладает (например, он там для проверки отказа
    /// кассиру) — заводим отдельного техника только для выдачи кода.
    /// </summary>
    public static async Task<InstallEnrollResponse> EnrollDeviceAsync(
        PlatformApiFactory factory,
        Guid organizationId,
        Guid branchId,
        string machineName = "PC-001",
        string? seatName = null,
        string? displayName = null,
        string? publicKey = null,
        int maxDevices = 100)
    {
        using var installerClient = factory.CreateClient();
        await AuthorizeInstallerAsync(factory, installerClient, organizationId, branchId);
        using var deviceClient = factory.CreateClient();
        return await EnrollCoreAsync(
            installerClient, deviceClient, organizationId, branchId, machineName, seatName, displayName, publicKey, maxDevices);
    }

    private static async Task<InstallEnrollResponse> EnrollCoreAsync(
        HttpClient installerClient,
        HttpClient deviceClient,
        Guid organizationId,
        Guid branchId,
        string machineName,
        string? seatName,
        string? displayName,
        string? publicKey,
        int maxDevices)
    {
        var issueResponse = await installerClient.PostAsJsonAsync(
            InstallCodeRoutes.Branch(organizationId, branchId),
            new CreateInstallCodeRequest(LifetimeHours: 24, maxDevices));
        Assert.True(issueResponse.IsSuccessStatusCode, await issueResponse.Content.ReadAsStringAsync());
        var issued = await issueResponse.Content.ReadFromJsonAsync<InstallCodeDto>();
        Assert.NotNull(issued);

        var enrollResponse = await deviceClient.PostAsJsonAsync(
            InstallRoutes.CodeEnroll,
            new InstallCodeEnrollRequest(
                issued.Code!,
                seatName,
                displayName,
                machineName,
                publicKey ?? $"test-key-{Guid.NewGuid():N}"));
        Assert.True(enrollResponse.IsSuccessStatusCode, await enrollResponse.Content.ReadAsStringAsync());
        var enrolled = await enrollResponse.Content.ReadFromJsonAsync<InstallEnrollResponse>();
        Assert.NotNull(enrolled);
        return enrolled;
    }

    /// <summary>
    /// Свой сотрудник-установщик для случая, когда у клиента теста уже есть роль без права ставить
    /// ПК. Организация и филиал должны быть уже посеяны кем-то из вызывающих (иначе вставлять
    /// некуда), а этот сотрудник лишь добавляется к ним.
    /// </summary>
    private static async Task AuthorizeInstallerAsync(
        PlatformApiFactory factory,
        HttpClient client,
        Guid organizationId,
        Guid branchId)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

            // Тесты, у которых не было своего сотрудника, тоже не заводили организацию и филиал —
            // старый путь их не спрашивал. Живой путь спрашивает: без них некуда ставить код.
            if (!await db.Organizations.AnyAsync(organization => organization.OrganizationId == organizationId))
            {
                db.Organizations.Add(new OrganizationEntity
                {
                    OrganizationId = organizationId,
                    Name = "Demo Org",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            }

            if (!await db.Branches.AnyAsync(branch => branch.BranchId == branchId))
            {
                db.Branches.Add(new BranchEntity
                {
                    BranchId = branchId,
                    OrganizationId = organizationId,
                    Name = "Demo Branch",
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
            }

            await db.SaveChangesAsync();

            if (!await db.StaffUsers.AnyAsync(user => user.StaffUserId == InstallerStaffUserId))
            {
                var hasher = new PasswordHasher<StaffUserEntity>();
                var user = new StaffUserEntity
                {
                    StaffUserId = InstallerStaffUserId,
                    OrganizationId = organizationId,
                    UserName = InstallerUserName,
                    NormalizedUserName = InstallerUserName.ToUpperInvariant(),
                    DisplayName = "Installer",
                    IsActive = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };
                user.PasswordHash = hasher.HashPassword(user, InstallerPassword);
                db.StaffUsers.Add(user);
                db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
                {
                    StaffRoleAssignmentId = Guid.NewGuid(),
                    StaffUserId = user.StaffUserId,
                    OrganizationId = organizationId,
                    BranchId = branchId,
                    RoleName = OrganizationRoleNames.Technician
                });
                await db.SaveChangesAsync();
            }
        }

        var signInResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId:D}/auth/staff/sign-in",
            new StaffSignInRequest(organizationId, InstallerUserName, InstallerPassword));
        Assert.True(signInResponse.IsSuccessStatusCode, await signInResponse.Content.ReadAsStringAsync());
        var body = await signInResponse.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
    }
}
