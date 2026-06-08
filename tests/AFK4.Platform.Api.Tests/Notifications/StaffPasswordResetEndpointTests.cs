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
    [GeneratedRegex(@"\b[0-9]{6}\b")]
    private static partial Regex CodePattern();

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

        // The 6-digit reset code is rendered into the outbox snapshot body even though SMTP is unconfigured.
        string code;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var row = await db.NotificationOutbox.SingleAsync(r => r.TemplateKey == NotificationTemplateKeys.StaffPasswordReset);
            code = CodePattern().Match(row.BodyText).Value;
            Assert.NotEmpty(code);
        }

        var reset = await client.PostAsJsonAsync("/api/auth/staff/reset-password",
            new StaffResetPasswordRequest("reset.owner@club.example", code, "BrandNewPass1"));
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
    public async Task Reset_WrongCode_Returns400WithRemainingAttempts()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedStaffAsync(factory, "reset.owner", "reset.owner@club.example", "OldPassw0rd");
        await client.PostAsJsonAsync("/api/auth/staff/forgot-password",
            new StaffForgotPasswordRequest("reset.owner@club.example"));

        var response = await client.PostAsJsonAsync("/api/auth/staff/reset-password",
            new StaffResetPasswordRequest("reset.owner@club.example", "000001", "BrandNewPass1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ResetErrorBody>();
        Assert.Equal("invalid_code", body!.Error);
        Assert.Equal(2, body.RemainingAttempts);
    }

    [Fact]
    public async Task Reset_NoPendingCode_Returns410()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedStaffAsync(factory, "reset.owner", "reset.owner@club.example", "OldPassw0rd");

        var response = await client.PostAsJsonAsync("/api/auth/staff/reset-password",
            new StaffResetPasswordRequest("reset.owner@club.example", "123456", "BrandNewPass1"));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Reset_ShortPassword_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/staff/reset-password",
            new StaffResetPasswordRequest("reset.owner@club.example", "123456", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record ResetErrorBody(string Error, int RemainingAttempts);
}
