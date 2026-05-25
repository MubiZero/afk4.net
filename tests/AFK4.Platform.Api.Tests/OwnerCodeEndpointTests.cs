using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Identity.OwnerCodes;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class OwnerCodeEndpointTests
{
    [Fact]
    public async Task GetOwnerCode_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/staff/me/owner-code");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOwnerCode_WithStaffWithoutOwnerPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);

        var response = await client.GetAsync("/api/staff/me/owner-code");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOwnerCode_WithoutActiveCode_ReturnsNoContent()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var response = await client.GetAsync("/api/staff/me/owner-code");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GenerateOwnerCode_HappyPath_Returns8DigitCodePersistsHashedRowAndAuditsSuccess()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var response = await client.PostAsync("/api/staff/me/owner-code/generate", content: null);
        var issued = await response.Content.ReadFromJsonAsync<OwnerCodeIssuedResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(issued);
        Assert.Equal(8, issued.OwnerCode.Length);
        Assert.True(issued.OwnerCode.All(char.IsDigit), $"owner code should be all digits but was '{issued.OwnerCode}'");
        Assert.Equal(issued.OwnerCode[^4..], issued.CodeSuffix);
        Assert.True(issued.ExpiresAtUtc > DateTimeOffset.UtcNow);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await dbContext.OwnerCodes
            .AsNoTracking()
            .SingleAsync(code => code.StaffUserId == TestIds.TechnicianStaffUserId);
        Assert.Equal(ExpectedHash(issued.OwnerCode), stored.CodeHash);
        Assert.Equal(issued.CodeSuffix, stored.CodeSuffix);
        Assert.Null(stored.RevokedAtUtc);
        Assert.Equal(0, stored.FailedAttemptCount);

        var audits = await dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.Action == AuditActionNames.GenerateOwnerCode)
            .ToListAsync();
        Assert.Single(audits);
        Assert.Equal(AuditOutcome.Succeeded, audits[0].Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audits[0].ActorStaffUserId);
    }

    [Fact]
    public async Task GenerateOwnerCode_WhenActiveCodeExists_ReturnsConflict()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var first = await client.PostAsync("/api/staff/me/owner-code/generate", content: null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsync("/api/staff/me/owner-code/generate", content: null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task RotateOwnerCode_RevokesPreviousAndReturnsNewCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var generated = await client.PostAsync("/api/staff/me/owner-code/generate", content: null);
        var generatedBody = await generated.Content.ReadFromJsonAsync<OwnerCodeIssuedResponse>();
        Assert.NotNull(generatedBody);

        var rotateResponse = await client.PostAsJsonAsync(
            "/api/staff/me/owner-code/rotate",
            new RotateOwnerCodeRequest(Reason: "test-rotation"));
        var rotated = await rotateResponse.Content.ReadFromJsonAsync<OwnerCodeIssuedResponse>();

        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);
        Assert.NotNull(rotated);
        Assert.Equal(8, rotated.OwnerCode.Length);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var allCodes = await dbContext.OwnerCodes
            .AsNoTracking()
            .Where(code => code.StaffUserId == TestIds.TechnicianStaffUserId)
            .ToListAsync();
        Assert.Equal(2, allCodes.Count);
        var revoked = allCodes.Single(code => code.RevokedAtUtc != null);
        var active = allCodes.Single(code => code.RevokedAtUtc == null);
        Assert.Equal(ExpectedHash(generatedBody.OwnerCode), revoked.CodeHash);
        Assert.Equal("test-rotation", revoked.RevokedReason);
        Assert.Equal(ExpectedHash(rotated.OwnerCode), active.CodeHash);
    }

    [Fact]
    public async Task GetOwnerCode_AfterGenerate_ReturnsSummaryWithSuffixOnly()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var generated = await client.PostAsync("/api/staff/me/owner-code/generate", content: null);
        var generatedBody = await generated.Content.ReadFromJsonAsync<OwnerCodeIssuedResponse>();
        Assert.NotNull(generatedBody);

        var summaryResponse = await client.GetAsync("/api/staff/me/owner-code");
        var summary = await summaryResponse.Content.ReadFromJsonAsync<OwnerCodeSummaryResponse?>();

        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        Assert.NotNull(summary);
        Assert.Equal(generatedBody.CodeSuffix, summary.CodeSuffix);
        Assert.Equal(generatedBody.ExpiresAtUtc, summary.ExpiresAtUtc);
        Assert.Equal(0, summary.FailedAttemptCount);
        Assert.Null(summary.LastUsedAtUtc);
    }

    [Fact]
    public async Task LookupActiveOwnerCode_WithIssuedCode_ReturnsStaffContextAndUpdatesLastUsed()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var generated = await client.PostAsync("/api/staff/me/owner-code/generate", content: null);
        var generatedBody = await generated.Content.ReadFromJsonAsync<OwnerCodeIssuedResponse>();
        Assert.NotNull(generatedBody);

        await using var scope = factory.Services.CreateAsyncScope();
        var ownerCodeService = scope.ServiceProvider.GetRequiredService<IOwnerCodeService>();
        var lookup = await ownerCodeService.LookupActiveAsync(generatedBody.OwnerCode, CancellationToken.None);

        Assert.Equal(OwnerCodeLookupStatus.Succeeded, lookup.Status);
        Assert.Equal(TestIds.TechnicianStaffUserId, lookup.StaffUserId);
        Assert.Equal(TestIds.OrganizationId, lookup.OrganizationId);

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await dbContext.OwnerCodes
            .AsNoTracking()
            .SingleAsync(code => code.StaffUserId == TestIds.TechnicianStaffUserId);
        Assert.NotNull(stored.LastUsedAtUtc);
    }

    private static string ExpectedHash(string plaintext)
    {
        var bytes = Encoding.ASCII.GetBytes(plaintext);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
