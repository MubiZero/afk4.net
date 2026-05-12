using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

internal static class StaffAuthTestHelper
{
    public static async Task AuthorizeAsAsync(
        PlatformApiFactory factory,
        HttpClient client,
        string roleName)
    {
        await SeedStaffAsync(factory, roleName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SignInAsync(client));
    }

    private static async Task<string> SignInAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "tech@afk4.test", "Passw0rd!"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
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
