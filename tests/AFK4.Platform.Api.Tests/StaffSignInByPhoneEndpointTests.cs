using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class StaffSignInByPhoneEndpointTests
{
    private static async Task<Guid> SeedStaffWithPhoneAsync(
        PlatformApiFactory factory,
        string normalizedPhone,
        bool verified,
        string password = "Passw0rd!")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staffUserId = Guid.NewGuid();
        var staff = new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = Guid.NewGuid(),
            UserName = $"u{normalizedPhone}",
            NormalizedUserName = $"U{normalizedPhone}",
            DisplayName = "Phone Staff",
            IsActive = true,
            Phone = "+" + normalizedPhone,
            NormalizedPhone = normalizedPhone,
            PhoneVerifiedAtUtc = verified ? DateTimeOffset.Parse("2026-06-01T00:00:00Z") : null,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
        };
        staff.PasswordHash = new PasswordHasher<StaffUserEntity>().HashPassword(staff, password);
        db.StaffUsers.Add(staff);
        await db.SaveChangesAsync();
        return staffUserId;
    }

    [Fact]
    public async Task SignInByPhone_VerifiedPhone_CorrectPassword_ReturnsToken()
    {
        await using var factory = new PlatformApiFactory();
        var staffUserId = await SeedStaffWithPhoneAsync(factory, "992937380070", verified: true);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest("+992 93 738-00-70", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.NotNull(body);
        Assert.Equal(staffUserId, body!.StaffUserId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task SignInByPhone_WrongPassword_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffWithPhoneAsync(factory, "992937380070", verified: true);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest("992937380070", "WRONG"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignInByPhone_UnverifiedPhone_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffWithPhoneAsync(factory, "992937380070", verified: false);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest("992937380070", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignInByPhone_UnknownPhone_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest("992000000000", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
