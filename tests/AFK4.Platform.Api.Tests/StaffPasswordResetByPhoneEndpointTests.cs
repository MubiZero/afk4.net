using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AFK4.Platform.Api.Data;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class StaffPasswordResetByPhoneEndpointTests
{
    private const string Phone = "992937380070";
    private const string OldPassword = "OldPassw0rd!";
    private const string NewPassword = "NewPassw0rd!";

    private sealed class RecordingSmsTransport : ISmsTransport
    {
        public List<SmsMessage> Sent { get; } = [];

        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static PlatformApiFactory CreateFactory(RecordingSmsTransport recording) =>
        new(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });

    private static async Task SeedVerifiedStaffAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staff = new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserName = "u" + Phone,
            NormalizedUserName = "U" + Phone,
            DisplayName = "Phone Staff",
            IsActive = true,
            Phone = "+" + Phone,
            NormalizedPhone = Phone,
            PhoneVerifiedAtUtc = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
        };
        staff.PasswordHash = new PasswordHasher<StaffUserEntity>().HashPassword(staff, OldPassword);
        db.StaffUsers.Add(staff);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Forgot_VerifiedPhone_ReturnsOk_AndSendsSms()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = CreateFactory(recording);
        await SeedVerifiedStaffAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest("+992 93 738-00-70"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sms = Assert.Single(recording.Sent);
        Assert.Equal("+992937380070", sms.ToPhoneNumber);
        Assert.Matches("\\d{6}", sms.Text);
    }

    [Fact]
    public async Task Forgot_UnknownPhone_ReturnsOk_ButSendsNothing()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = CreateFactory(recording);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest("992000000000"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(recording.Sent);
    }

    [Fact]
    public async Task Forgot_InvalidPhone_ReturnsBadRequest()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = CreateFactory(recording);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest("12345"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_WrongCode_ReturnsBadRequest()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = CreateFactory(recording);
        await SeedVerifiedStaffAsync(factory);
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest(Phone));

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/reset-password-by-phone",
            new StaffResetPasswordByPhoneRequest(Phone, "000000", NewPassword));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_WeakPassword_ReturnsBadRequest()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = CreateFactory(recording);
        await SeedVerifiedStaffAsync(factory);
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest(Phone));
        var code = Regex.Match(Assert.Single(recording.Sent).Text, "\\d{6}").Value;

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/reset-password-by-phone",
            new StaffResetPasswordByPhoneRequest(Phone, code, "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_CorrectCode_ChangesPassword_NewPasswordSignsIn_OldFails()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = CreateFactory(recording);
        await SeedVerifiedStaffAsync(factory);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest(Phone));
        var code = Regex.Match(Assert.Single(recording.Sent).Text, "\\d{6}").Value;

        var reset = await client.PostAsJsonAsync(
            "/api/auth/staff/reset-password-by-phone",
            new StaffResetPasswordByPhoneRequest(Phone, code, NewPassword));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var withNew = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest(Phone, NewPassword));
        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);

        var withOld = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest(Phone, OldPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, withOld.StatusCode);
    }
}
