using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Сотрудник клуба заводится без разработчика.
///
/// До сих пор «Сотрудники и роли» выдавали код приглашения, принять который было негде: маршрут
/// существовал, экрана не было, и в инструкции по запуску прямо написано заводить людей
/// PowerShell-скриптом с паролем. Это ровно то, чего в пилоте быть не должно.
///
/// Приглашение идёт на телефон, потому что телефон — это то, что у администратора зала есть
/// наверняка и то, чем он потом входит. Почта у него может не существовать вовсе.
/// </summary>
public sealed class StaffInviteByPhoneTests
{
    private const string Phone = "+992937380070";
    private const string Password = "FreshPass123";

    [Fact]
    public async Task Invite_SendsAShortCodeToThePhone()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var create = await InviteAsync(client);

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var code = await ReadCodeFromSmsAsync(factory);
        // Шесть цифр: человек переносит их с телефона в форму на экране клуба, а не копирует
        // строку в сто символов.
        Assert.Equal(6, code.Length);
        Assert.All(code, character => Assert.True(char.IsAsciiDigit(character)));
    }

    /// <summary>
    /// Главное: после приёма человек входит по номеру — тем же способом, что и все остальные
    /// сотрудники. Приглашение, оставляющее человека без телефона, оставляет его и без входа.
    /// </summary>
    [Fact]
    public async Task Accept_MakesAStaffWhoSignsInByPhone()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        var code = await ReadCodeFromSmsAsync(factory);

        var accept = await client.PostAsJsonAsync(
            "/api/staff/invites/accept", new AcceptStaffInviteRequest(Phone, code, Password));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var signIn = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest(Phone, Password));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task Accept_GivesTheInvitedRoles()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        var code = await ReadCodeFromSmsAsync(factory);

        await client.PostAsJsonAsync(
            "/api/staff/invites/accept", new AcceptStaffInviteRequest(Phone, code, Password));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staff = await db.StaffUsers.SingleAsync(user => user.NormalizedPhone == "992937380070");
        Assert.NotNull(staff.PhoneVerifiedAtUtc);
        Assert.True(staff.IsActive);
        var roles = await db.StaffRoleAssignments
            .Where(assignment => assignment.StaffUserId == staff.StaffUserId)
            .Select(assignment => assignment.RoleName)
            .ToListAsync();
        Assert.Equal([OrganizationRoleNames.Operator], roles);
    }

    /// <summary>
    /// Шесть цифр перебираются за вечер, если пробовать бесконечно. Три попытки — и приглашение
    /// мертво; владелец приглашает заново, это одно нажатие.
    /// </summary>
    [Fact]
    public async Task Accept_WithWrongCodes_RunsOutOfAttempts()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        var code = await ReadCodeFromSmsAsync(factory);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var wrong = await client.PostAsJsonAsync(
                "/api/staff/invites/accept", new AcceptStaffInviteRequest(Phone, "000000", Password));
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        // Даже верный код после этого не пускает — иначе счётчик попыток не значит ничего.
        var afterwards = await client.PostAsJsonAsync(
            "/api/staff/invites/accept", new AcceptStaffInviteRequest(Phone, code, Password));
        Assert.Equal(HttpStatusCode.TooManyRequests, afterwards.StatusCode);
    }

    [Fact]
    public async Task Accept_AfterTheInviteExpired_SaysTheCodeIsDead()
    {
        var time = new Identity.MovableTimeProvider(DateTimeOffset.Parse("2026-09-01T10:00:00Z"));
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(time);
        });
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        var code = await ReadCodeFromSmsAsync(factory);

        time.Advance(TimeSpan.FromHours(25));
        var accept = await client.PostAsJsonAsync(
            "/api/staff/invites/accept", new AcceptStaffInviteRequest(Phone, code, Password));

        Assert.Equal(HttpStatusCode.Gone, accept.StatusCode);
    }

    /// <summary>
    /// Приглашение выслали заново — старый код умирает. Два живых кода на один номер означают,
    /// что отозвать ошибочное приглашение нечем.
    /// </summary>
    [Fact]
    public async Task InvitingAgain_KillsThePreviousCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        var first = await ReadCodeFromSmsAsync(factory);

        await InviteAsync(client);

        var accept = await client.PostAsJsonAsync(
            "/api/staff/invites/accept", new AcceptStaffInviteRequest(Phone, first, Password));
        Assert.NotEqual(HttpStatusCode.OK, accept.StatusCode);
    }

    /// <summary>Приглашение на номер, который уже работает в этой сети, — это ошибка, а не второй счёт.</summary>
    [Fact]
    public async Task Invite_ToAPhoneThatAlreadyWorksHere_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        var code = await ReadCodeFromSmsAsync(factory);
        await client.PostAsJsonAsync(
            "/api/staff/invites/accept", new AcceptStaffInviteRequest(Phone, code, Password));

        var again = await InviteAsync(client, userName: "second.cashier");

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    private static Task<HttpResponseMessage> InviteAsync(HttpClient client, string userName = "new.cashier") =>
        client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/staff/invites",
            new CreateStaffInviteRequest(
                TestIds.OrganizationId, userName, "Новый администратор", Phone, null,
                [OrganizationRoleNames.Operator]));

    private static async Task<string> ReadCodeFromSmsAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var body = await db.NotificationOutbox
            .Where(row => row.TemplateKey == NotificationTemplateKeys.StaffInviteSms)
            .OrderByDescending(row => row.CreatedUtc)
            .Select(row => row.BodyText)
            .FirstAsync();
        var code = System.Text.RegularExpressions.Regex.Match(body, @"\b\d{6}\b").Value;
        Assert.NotEmpty(code);
        return code;
    }
}
