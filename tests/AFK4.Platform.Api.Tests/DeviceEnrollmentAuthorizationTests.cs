using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceEnrollmentAuthorizationTests
{
    [Fact]
    public async Task PostDeviceEnrollmentCode_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostDeviceEnrollmentCode_WithTechnicianPermission_CreatesCodeAndAuditRecord()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffAsync(factory, StaffRoleNames.Technician);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SignInAsync(client));

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300));
        var code = await response.Content.ReadFromJsonAsync<DeviceEnrollmentCodeDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(code);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.CreateDeviceEnrollmentCode, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
        Assert.Equal(code.Code, audit.TargetId);
    }

    [Fact]
    public async Task PostDeviceEnrollmentCode_WithCashierRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffAsync(factory, StaffRoleNames.CashierOperator);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SignInAsync(client));

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
    }

    private static async Task<string> SignInAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "tech@afk4.test", "Passw0rd!"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        return body.AccessToken;
    }

    private static async Task SeedStaffAsync(PlatformApiFactory factory, string roleName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var hasher = new PasswordHasher<StaffUserEntity>();
        var createdAt = DateTimeOffset.Parse("2026-05-12T00:00:00Z");
        var user = new StaffUserEntity
        {
            StaffUserId = TestIds.TechnicianStaffUserId,
            OrganizationId = TestIds.OrganizationId,
            UserName = "tech@afk4.test",
            NormalizedUserName = "TECH@AFK4.TEST",
            DisplayName = "Tech One",
            IsActive = true,
            CreatedAtUtc = createdAt
        };
        user.PasswordHash = hasher.HashPassword(user, "Passw0rd!");

        dbContext.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Org",
            CreatedAtUtc = createdAt
        });
        dbContext.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Branch",
            CreatedAtUtc = createdAt
        });
        dbContext.StaffUsers.Add(user);
        dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = user.StaffUserId,
            OrganizationId = user.OrganizationId,
            BranchId = TestIds.BranchId,
            RoleName = roleName
        });
        await dbContext.SaveChangesAsync();
    }
}
