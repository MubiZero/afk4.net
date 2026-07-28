using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class StaffPhoneVerificationEndpointTests
{
    private sealed class RecordingSmsTransport : ISmsTransport
    {
        public List<SmsMessage> Sent { get; } = [];

        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task StartThenConfirm_VerifiesStaffPhone()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });
        using var client = factory.CreateClient();
        // AuthorizeAsAsync returns Task (void) — the seeded staff user id is TestIds.TechnicianStaffUserId
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var staffUserId = TestIds.TechnicianStaffUserId;

        var start = await client.PostAsJsonAsync(
            "/api/auth/staff/phone/start-verification",
            new StaffPhoneStartVerificationRequest("+992 93 738-00-70"));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        var sms = Assert.Single(recording.Sent);
        Assert.Equal("+992937380070", sms.ToPhoneNumber);
        var code = Regex.Match(sms.Text, "\\d{6}").Value;
        Assert.False(string.IsNullOrEmpty(code));

        var confirm = await client.PostAsJsonAsync(
            "/api/auth/staff/phone/confirm",
            new StaffPhoneConfirmRequest(code));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staff = db.StaffUsers.Single(user => user.StaffUserId == staffUserId);
        Assert.Equal("992937380070", staff.NormalizedPhone);
        Assert.NotNull(staff.PhoneVerifiedAtUtc);
    }

    [Fact]
    public async Task StartVerification_WithoutBearer_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/phone/start-verification",
            new StaffPhoneStartVerificationRequest("992937380070"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPhone_BeforeVerification_ReturnsNulls()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        var response = await client.GetAsync("/api/auth/staff/phone");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<StaffPhoneStatusResponse>();
        Assert.NotNull(status);
        Assert.Null(status!.Phone);
        Assert.Null(status.PhoneVerifiedAtUtc);
    }

    [Fact]
    public async Task GetPhone_WithoutBearer_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/staff/phone");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPhone_AfterVerification_ReturnsVerifiedPhone()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        var startResp = await client.PostAsJsonAsync(
            "/api/auth/staff/phone/start-verification",
            new StaffPhoneStartVerificationRequest("+992 93 738-00-70"));
        Assert.Equal(HttpStatusCode.OK, startResp.StatusCode);

        var code = Regex.Match(Assert.Single(recording.Sent).Text, "\\d{6}").Value;

        var confirmResp = await client.PostAsJsonAsync(
            "/api/auth/staff/phone/confirm", new StaffPhoneConfirmRequest(code));
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);

        var response = await client.GetAsync("/api/auth/staff/phone");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<StaffPhoneStatusResponse>();
        Assert.NotNull(status);
        Assert.Equal("+992937380070", status!.Phone);
        Assert.NotNull(status.PhoneVerifiedAtUtc);
    }
}
