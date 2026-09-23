using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

// Сотрудник и филиалы сети. Роли раньше менялись только там, где человек уже назначен: во второй
// филиал его было не добавить (приглашение на тот же телефон отказывает), с филиала — не снять
// (пустой набор ролей не сохраняется), а того, у кого назначений не осталось, не показывал ни
// один список. И сохранение ролей молча стирало владельцу его роль в филиале.
public sealed class StaffBranchAssignmentEndpointTests
{
    private static readonly Guid SecondBranchId = Guid.Parse("5e1c0a4e-2b1f-4f55-9a7e-3d6f0c2b9a11");

    private static string Staff(Guid branchId) =>
        $"/api/organizations/{TestIds.OrganizationId:D}/branches/{branchId:D}/staff";

    private static async Task SeedSecondBranchAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Branches.Add(new BranchEntity
        {
            BranchId = SecondBranchId,
            OrganizationId = TestIds.OrganizationId,
            Name = "Second Branch",
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedStaffAsync(
        PlatformApiFactory factory, string userName, params (Guid BranchId, string Role)[] assignments)
    {
        var staffUserId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = TestIds.OrganizationId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = userName,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        });
        foreach (var (branchId, role) in assignments)
        {
            db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(),
                StaffUserId = staffUserId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = branchId,
                RoleName = role
            });
        }

        await db.SaveChangesAsync();
        return staffUserId;
    }

    private static async Task<List<string>> RolesAsync(PlatformApiFactory factory, Guid staffUserId, Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.StaffRoleAssignments
            .Where(row => row.StaffUserId == staffUserId && row.BranchId == branchId)
            .Select(row => row.RoleName)
            .OrderBy(role => role)
            .ToListAsync();
    }

    // Клиент создаётся в самом тесте, а не здесь: фабрика запоминает имя своей базы в AsyncLocal при
    // первом CreateClient, и значение, заданное внутри вспомогательного async-метода, в тест не
    // возвращается — запросы уходили бы в пустую базу.
    private static async Task SignInAsOwnerAsync(PlatformApiFactory factory, HttpClient client)
    {
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await SeedSecondBranchAsync(factory);
    }

    [Fact]
    public async Task UpdateRoles_ForAnOwner_KeepsTheOwnerRoleInThatBranch()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SignInAsOwnerAsync(factory, client);
        var otherOwner = await SeedStaffAsync(factory, "co.owner", (TestIds.BranchId, OrganizationRoleNames.OrganizationOwner));

        var response = await client.PatchAsJsonAsync(
            $"{Staff(TestIds.BranchId)}/{otherOwner:D}/roles",
            new UpdateStaffUserRolesRequest(TestIds.OrganizationId, [OrganizationRoleNames.BranchManager]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            [OrganizationRoleNames.BranchManager, OrganizationRoleNames.OrganizationOwner],
            await RolesAsync(factory, otherOwner, TestIds.BranchId));
    }

    [Fact]
    public async Task UpdateRoles_ForSomeoneFromAnotherBranch_AddsThemToThisBranch()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SignInAsOwnerAsync(factory, client);
        var cashier = await SeedStaffAsync(factory, "cashier.two", (SecondBranchId, OrganizationRoleNames.Operator));

        var response = await client.PatchAsJsonAsync(
            $"{Staff(TestIds.BranchId)}/{cashier:D}/roles",
            new UpdateStaffUserRolesRequest(TestIds.OrganizationId, [OrganizationRoleNames.ShiftSupervisor]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([OrganizationRoleNames.ShiftSupervisor], await RolesAsync(factory, cashier, TestIds.BranchId));
        Assert.Equal([OrganizationRoleNames.Operator], await RolesAsync(factory, cashier, SecondBranchId));
        var list = await client.GetFromJsonAsync<List<StaffUserDto>>(Staff(TestIds.BranchId));
        Assert.Contains(list!, user => user.StaffUserId == cashier);
    }

    [Fact]
    public async Task UpdateRoles_AddingToABranch_RespectsThePlanLimitOnStaff()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SignInAsOwnerAsync(factory, client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var organization = await db.Organizations.SingleAsync(row => row.OrganizationId == TestIds.OrganizationId);
            // В филиале уже один сотрудник — сам владелец; лимит тарифа — один.
            organization.LimitsJson = OrganizationLimitsJson.Serialize(new OrganizationLimitsDto(null, null, null, 1));
            await db.SaveChangesAsync();
        }

        var cashier = await SeedStaffAsync(factory, "cashier.limit", (SecondBranchId, OrganizationRoleNames.Operator));

        var response = await client.PatchAsJsonAsync(
            $"{Staff(TestIds.BranchId)}/{cashier:D}/roles",
            new UpdateStaffUserRolesRequest(TestIds.OrganizationId, [OrganizationRoleNames.Operator]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(PlanLimitNames.StaffUsersPerBranch, await response.Content.ReadAsStringAsync());
        Assert.Empty(await RolesAsync(factory, cashier, TestIds.BranchId));
    }

    [Fact]
    public async Task Candidates_ListTheOrganizationsStaffOutsideThisBranch_WithTheirBranches()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SignInAsOwnerAsync(factory, client);
        var elsewhere = await SeedStaffAsync(factory, "elsewhere", (SecondBranchId, OrganizationRoleNames.Operator));
        var unassigned = await SeedStaffAsync(factory, "unassigned");
        var here = await SeedStaffAsync(factory, "here", (TestIds.BranchId, OrganizationRoleNames.Operator));

        var candidates = await client.GetFromJsonAsync<List<StaffBranchCandidateDto>>($"{Staff(TestIds.BranchId)}/candidates");

        Assert.NotNull(candidates);
        Assert.Equal(["Second Branch"], candidates.Single(row => row.StaffUserId == elsewhere).BranchNames);
        Assert.Empty(candidates.Single(row => row.StaffUserId == unassigned).BranchNames);
        Assert.DoesNotContain(candidates, row => row.StaffUserId == here);
        Assert.DoesNotContain(candidates, row => row.StaffUserId == TestIds.TechnicianStaffUserId);
    }

    [Fact]
    public async Task Candidates_AreForTheOwnerOnly()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var response = await client.GetAsync($"{Staff(TestIds.BranchId)}/candidates");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RemoveFromBranch_TakesThePersonOffThisBranch_AndTheyCanBeReturned()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SignInAsOwnerAsync(factory, client);
        var cashier = await SeedStaffAsync(factory, "cashier.leaving", (TestIds.BranchId, OrganizationRoleNames.Operator));

        var removed = await client.DeleteAsync($"{Staff(TestIds.BranchId)}/{cashier:D}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(await RolesAsync(factory, cashier, TestIds.BranchId));
        var list = await client.GetFromJsonAsync<List<StaffUserDto>>(Staff(TestIds.BranchId));
        Assert.DoesNotContain(list!, user => user.StaffUserId == cashier);
        var candidates = await client.GetFromJsonAsync<List<StaffBranchCandidateDto>>($"{Staff(TestIds.BranchId)}/candidates");
        Assert.Empty(candidates!.Single(row => row.StaffUserId == cashier).BranchNames);

        var returned = await client.PatchAsJsonAsync(
            $"{Staff(TestIds.BranchId)}/{cashier:D}/roles",
            new UpdateStaffUserRolesRequest(TestIds.OrganizationId, [OrganizationRoleNames.Operator]));
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        Assert.Equal([OrganizationRoleNames.Operator], await RolesAsync(factory, cashier, TestIds.BranchId));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Contains(await db.AuditRecords.ToListAsync(), audit =>
            audit.Action == AuditActionNames.RemoveStaffFromBranch &&
            audit.TargetId == cashier.ToString("D") &&
            audit.Outcome == AuditOutcome.Succeeded);
    }

    [Fact]
    public async Task RemoveFromBranch_RefusesYourselfAndAnOwner()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SignInAsOwnerAsync(factory, client);
        var otherOwner = await SeedStaffAsync(factory, "co.owner.two", (TestIds.BranchId, OrganizationRoleNames.OrganizationOwner));

        var self = await client.DeleteAsync($"{Staff(TestIds.BranchId)}/{TestIds.TechnicianStaffUserId:D}");
        var owner = await client.DeleteAsync($"{Staff(TestIds.BranchId)}/{otherOwner:D}");

        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, owner.StatusCode);
        Assert.Equal([OrganizationRoleNames.OrganizationOwner], await RolesAsync(factory, otherOwner, TestIds.BranchId));
    }

    [Fact]
    public async Task RemoveFromBranch_IsForTheOwnerOnly()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var cashier = await SeedStaffAsync(factory, "cashier.stays", (TestIds.BranchId, OrganizationRoleNames.Operator));

        var response = await client.DeleteAsync($"{Staff(TestIds.BranchId)}/{cashier:D}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal([OrganizationRoleNames.Operator], await RolesAsync(factory, cashier, TestIds.BranchId));
    }
}
