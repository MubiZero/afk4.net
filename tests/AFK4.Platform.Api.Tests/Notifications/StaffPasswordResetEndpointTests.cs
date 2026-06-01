using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed partial class StaffPasswordResetEndpointTests
{
    [GeneratedRegex(@"[0-9a-fA-F]{32}\.[0-9A-Fa-f]{64}")]
    private static partial Regex TokenPattern();

    private static async Task<Guid> SeedStaffAsync(PlatformApiFactory factory, string userName, string email, string password)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staffUserId = Guid.NewGuid();
        var staff = new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = Guid.NewGuid(),
            UserName = userName,
            NormalizedUserName = userName.Trim().ToUpperInvariant(),
            DisplayName = "Reset Target",
            Email = email,
        };
        staff.PasswordHash = new PasswordHasher<StaffUserEntity>().HashPassword(staff, password);
        db.StaffUsers.Add(staff);
        await db.SaveChangesAsync();
        return staffUserId;
    }

    [Fact]
    public async Task ForgotThenReset_UpdatesPasswordEndToEnd()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var staffUserId = await SeedStaffAsync(factory, "reset.owner", "reset.owner@club.example", "OldPassw0rd");

        var forgot = await client.PostAsJsonAsync("/api/auth/staff/forgot-password",
            new StaffForgotPasswordRequest("reset.owner@club.example"));
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);

        // The reset token is rendered into the outbox snapshot body even though SMTP is unconfigured.
        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var row = await db.NotificationOutbox.SingleAsync(r => r.TemplateKey == NotificationTemplateKeys.StaffPasswordReset);
            token = TokenPattern().Match(row.BodyText).Value;
            Assert.NotEmpty(token);
        }

        var reset = await client.PostAsJsonAsync("/api/auth/staff/reset-password",
            new StaffResetPasswordRequest(token, "BrandNewPass1"));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var staff = await db.StaffUsers.SingleAsync(u => u.StaffUserId == staffUserId);
            var verification = new PasswordHasher<StaffUserEntity>().VerifyHashedPassword(staff, staff.PasswordHash, "BrandNewPass1");
            Assert.Equal(PasswordVerificationResult.Success, verification);
            var resetToken = await db.PasswordResetTokens.SingleAsync();
            Assert.NotNull(resetToken.ConsumedAtUtc);
        }
    }

    [Fact]
    public async Task Forgot_UnknownUser_Returns200WithoutIssuingToken()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/staff/forgot-password",
            new StaffForgotPasswordRequest("nobody@nowhere.example"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(0, await db.PasswordResetTokens.CountAsync());
    }

    [Fact]
    public async Task Reset_InvalidToken_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/staff/reset-password",
            new StaffResetPasswordRequest("00000000000000000000000000000000.deadbeef", "BrandNewPass1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_ShortPassword_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/staff/reset-password",
            new StaffResetPasswordRequest("00000000000000000000000000000000.deadbeef", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
