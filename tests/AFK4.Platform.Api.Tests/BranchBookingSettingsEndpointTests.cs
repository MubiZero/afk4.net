using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Branches;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Настройки приёма гостей: как филиал решает, брать ли брони из приложения и на каких условиях.
/// Филиал, который ничего не настраивал, отвечает значениями по умолчанию, а не пустотой.
/// </summary>
public sealed class BranchBookingSettingsEndpointTests
{
    private static string Route =>
        $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/booking-settings";

    private static UpdateBranchBookingSettingsRequest ValidRequest(
        string acceptanceMode = BranchBookingAcceptanceModes.Manual,
        int respondWithinMinutes = 30,
        bool requirePrepaymentFromNewGuests = false,
        int maxActiveReservationsForNewGuests = 2,
        int regularAfterVisits = 5,
        int holdSeatAfterStartMinutes = 25,
        bool keepPrepaymentOnNoShow = true,
        Guid? organizationId = null) =>
        new(
            organizationId ?? TestIds.OrganizationId,
            acceptanceMode,
            respondWithinMinutes,
            requirePrepaymentFromNewGuests,
            maxActiveReservationsForNewGuests,
            regularAfterVisits,
            holdSeatAfterStartMinutes,
            keepPrepaymentOnNoShow);

    [Fact]
    public async Task Get_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithStaffWithoutPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_ForBranchThatNeverConfiguredAnything_ReturnsDefaults()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.GetAsync(Route);
        var settings = await response.Content.ReadFromJsonAsync<BranchBookingSettingsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(settings);
        Assert.Equal(TestIds.BranchId, settings.BranchId);
        Assert.Equal(BranchBookingAcceptanceModes.Auto, settings.AcceptanceMode);
        Assert.Equal(15, settings.RespondWithinMinutes);
        Assert.True(settings.RequirePrepaymentFromNewGuests);
        Assert.Equal(1, settings.MaxActiveReservationsForNewGuests);
        Assert.Equal(3, settings.RegularAfterVisits);
        Assert.Equal(20, settings.HoldSeatAfterStartMinutes);
        Assert.False(settings.KeepPrepaymentOnNoShow);
        Assert.Null(settings.UpdatedAtUtc);
    }

    [Fact]
    public async Task Put_PersistsTheClubDecision_AndReadsBackTheSame()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.PutAsJsonAsync(Route, ValidRequest());
        var updated = await response.Content.ReadFromJsonAsync<BranchBookingSettingsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(BranchBookingAcceptanceModes.Manual, updated.AcceptanceMode);
        Assert.Equal(30, updated.RespondWithinMinutes);
        Assert.False(updated.RequirePrepaymentFromNewGuests);
        Assert.Equal(2, updated.MaxActiveReservationsForNewGuests);
        Assert.Equal(5, updated.RegularAfterVisits);
        Assert.Equal(25, updated.HoldSeatAfterStartMinutes);
        Assert.True(updated.KeepPrepaymentOnNoShow);
        Assert.NotNull(updated.UpdatedAtUtc);

        var readBack = await client.GetFromJsonAsync<BranchBookingSettingsDto>(Route);
        Assert.Equal(updated, readBack);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persisted = await dbContext.BranchBookingSettings.AsNoTracking()
            .SingleAsync(row => row.BranchId == TestIds.BranchId);
        Assert.Equal(BranchBookingAcceptanceModes.Manual, persisted.AcceptanceMode);
        Assert.NotEqual(Guid.Empty, persisted.UpdatedByStaffUserId);
    }

    [Fact]
    public async Task Put_Twice_UpdatesTheSameRow()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        (await client.PutAsJsonAsync(Route, ValidRequest())).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync(Route, ValidRequest(BranchBookingAcceptanceModes.Off))).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = await dbContext.BranchBookingSettings.AsNoTracking()
            .Where(row => row.BranchId == TestIds.BranchId)
            .ToListAsync();
        Assert.Single(rows);
        Assert.Equal(BranchBookingAcceptanceModes.Off, rows[0].AcceptanceMode);
    }

    [Fact]
    public async Task Put_WritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        (await client.PutAsJsonAsync(Route, ValidRequest())).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.AsNoTracking()
            .SingleAsync(record => record.Action == AuditActionNames.UpdateBookingSettings);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(TestIds.BranchId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task Put_WithStaffWithoutPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        var response = await client.PutAsJsonAsync(Route, ValidRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithMismatchedOrganization_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.PutAsJsonAsync(Route, ValidRequest(organizationId: Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("sometimes", 30, 2, 5, 25)]
    [InlineData(BranchBookingAcceptanceModes.Auto, 0, 2, 5, 25)]
    [InlineData(BranchBookingAcceptanceModes.Auto, 30, 0, 5, 25)]
    [InlineData(BranchBookingAcceptanceModes.Auto, 30, 2, -1, 25)]
    [InlineData(BranchBookingAcceptanceModes.Auto, 30, 2, 5, -5)]
    public async Task Put_WithValueOutsideItsRange_ReturnsBadRequest(
        string acceptanceMode,
        int respondWithinMinutes,
        int maxActiveReservations,
        int regularAfterVisits,
        int holdSeatAfterStartMinutes)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.PutAsJsonAsync(Route, ValidRequest(
            acceptanceMode,
            respondWithinMinutes,
            maxActiveReservationsForNewGuests: maxActiveReservations,
            regularAfterVisits: regularAfterVisits,
            holdSeatAfterStartMinutes: holdSeatAfterStartMinutes));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ForBranchOfAnotherOrganization_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.GetAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{Guid.NewGuid():D}/booking-settings");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
            $"Ожидался отказ по чужому филиалу, получено {response.StatusCode}.");
    }
}
