using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

// Регресс: право менеджера филиала A НЕ должно протекать на филиал B, где у того же
// сотрудника только кассирская роль. До фикса RequireBranchPermissionAsync проверял
// branchId-членство и право отдельно (union по всем ролям) — это давало эскалацию.
public class StaffAuthorizationServiceTests
{
    private static readonly Guid OrganizationId = Guid.Parse("2f6a7c9e-3b1d-4d5a-9c2e-1a2b3c4d5e6f");
    private static readonly Guid BranchAId = Guid.Parse("aaaaaaaa-1111-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid BranchBId = Guid.Parse("bbbbbbbb-2222-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid StaffUserId = Guid.Parse("cccccccc-3333-4ccc-8ccc-cccccccccccc");

    private static UpdateBranchProfileRequest ProfileRequest(Guid orgId) => new(
        OrganizationId: orgId,
        Name: "AFK4 Центр",
        City: "Душанбе",
        Description: "Лучший клуб",
        Address: "ул. Рудаки, 1",
        Phone: "+992900000000",
        Telegram: "afk4club",
        Website: "https://afk4.net",
        Instagram: "afk4.club",
        LogoUrl: null,
        LogoMediaId: null,
        TimeZone: "Asia/Dushanbe",
        Locale: "ru",
        WorkingHours: Enumerable.Range(1, 7)
            .Select(d => new BranchWorkingHoursDayDto(d, d == 7, d == 7 ? null : "10:00", d == 7 ? null : "23:00"))
            .ToList());

    [Fact]
    public async Task Manager_permission_does_not_leak_across_branches()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var accessToken = await SeedTwoBranchStaffAndSignInAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var onA = await client.PatchAsJsonAsync(
            $"/api/branches/{BranchAId}/profile", ProfileRequest(OrganizationId));
        var onB = await client.PatchAsJsonAsync(
            $"/api/branches/{BranchBId}/profile", ProfileRequest(OrganizationId));

        Assert.Equal(HttpStatusCode.OK, onA.StatusCode);         // менеджер на A — разрешено
        Assert.Equal(HttpStatusCode.Forbidden, onB.StatusCode);  // кассир на B — НЕ протекает
    }

    private static async Task<string> SeedTwoBranchStaffAndSignInAsync(PlatformApiFactory factory, HttpClient client)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var hasher = new PasswordHasher<StaffUserEntity>();
            var createdAt = DateTimeOffset.Parse("2026-05-12T00:00:00Z");
            var user = new StaffUserEntity
            {
                StaffUserId = StaffUserId,
                OrganizationId = OrganizationId,
                UserName = "two-branch@afk4.test",
                NormalizedUserName = "TWO-BRANCH@AFK4.TEST",
                DisplayName = "Two Branch Staff",
                IsActive = true,
                CreatedAtUtc = createdAt
            };
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!");

            dbContext.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = OrganizationId,
                Name = "Two Branch Org",
                CreatedAtUtc = createdAt
            });
            dbContext.Branches.Add(new BranchEntity
            {
                BranchId = BranchAId,
                OrganizationId = OrganizationId,
                Name = "Branch A",
                CreatedAtUtc = createdAt
            });
            dbContext.Branches.Add(new BranchEntity
            {
                BranchId = BranchBId,
                OrganizationId = OrganizationId,
                Name = "Branch B",
                CreatedAtUtc = createdAt
            });
            dbContext.StaffUsers.Add(user);
            dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(),
                StaffUserId = user.StaffUserId,
                OrganizationId = user.OrganizationId,
                BranchId = BranchAId,
                RoleName = StaffRoleNames.BranchManager
            });
            dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(),
                StaffUserId = user.StaffUserId,
                OrganizationId = user.OrganizationId,
                BranchId = BranchBId,
                RoleName = StaffRoleNames.CashierOperator
            });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(OrganizationId, "two-branch@afk4.test", "Passw0rd!"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        return body!.AccessToken;
    }
}
