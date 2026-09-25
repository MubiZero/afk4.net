using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
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
    private const string Password = "654321";

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

    /// <summary>
    /// Вход в два шага: номер, потом то, что у этого номера есть. Новому сотруднику — код первого
    /// входа, а не «придумайте ПИН»: номер не секрет, и без кода ПИН успел бы назначить любой,
    /// кто этот номер знает.
    /// </summary>
    [Fact]
    public async Task FirstStep_ForAnInvitedPhone_AsksForTheInviteCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);

        Assert.Equal(StaffSignInStepNames.InviteCode, await NextStepAsync(client, "+992 93 738 00 70"));
    }

    [Fact]
    public async Task FirstStep_OnceThePinIsSet_AsksForThePin()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        await client.PostAsJsonAsync(
            StaffAuthRoutes.AcceptInvite, new AcceptStaffInviteRequest(Phone, await ReadCodeFromSmsAsync(factory), Password));

        Assert.Equal(StaffSignInStepNames.Pin, await NextStepAsync(client, Phone));
    }

    [Fact]
    public async Task FirstStep_ForANumberNoClubAdded_SaysSo()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(StaffSignInStepNames.Unknown, await NextStepAsync(client, "+992 90 000 00 01"));
        Assert.Equal(StaffSignInStepNames.Unknown, await NextStepAsync(client, "не номер"));
    }

    [Fact]
    public async Task FirstStep_AfterTheCodeDied_AsksForANewOne()
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

        time.Advance(TimeSpan.FromHours(25));

        Assert.Equal(StaffSignInStepNames.InviteExpired, await NextStepAsync(client, Phone));
    }

    [Fact]
    public async Task FirstStep_AfterThreeWrongCodes_AsksForANewOne()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await client.PostAsJsonAsync(StaffAuthRoutes.CheckInvite, new CheckStaffInviteRequest(Phone, "000000"));
        }

        Assert.Equal(StaffSignInStepNames.InviteExpired, await NextStepAsync(client, Phone));
    }

    /// <summary>Код проверяется до ПИНа, и верная проверка приглашение не тратит.</summary>
    [Fact]
    public async Task CheckingTheRightCode_LeavesTheInviteForThePin()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        var code = await ReadCodeFromSmsAsync(factory);

        var check = await client.PostAsJsonAsync(StaffAuthRoutes.CheckInvite, new CheckStaffInviteRequest(Phone, code));
        Assert.Equal(HttpStatusCode.NoContent, check.StatusCode);
        Assert.Equal(StaffSignInStepNames.InviteCode, await NextStepAsync(client, Phone));

        var accept = await client.PostAsJsonAsync(StaffAuthRoutes.AcceptInvite, new AcceptStaffInviteRequest(Phone, code, Password));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
    }

    [Fact]
    public async Task CheckingAWrongCode_SpendsAnAttempt()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);

        var check = await client.PostAsJsonAsync(StaffAuthRoutes.CheckInvite, new CheckStaffInviteRequest(Phone, "000000"));

        Assert.Equal(HttpStatusCode.BadRequest, check.StatusCode);
        var body = await check.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("invalid_code", body.GetProperty("error").GetString());
        Assert.Equal(2, body.GetProperty("remainingAttempts").GetInt32());
    }

    /// <summary>Придумал ПИН — уже внутри: второй раз вводить его сразу же незачем.</summary>
    [Fact]
    public async Task Accept_SignsTheNewStaffInRightAway()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        var code = await ReadCodeFromSmsAsync(factory);

        var accept = await client.PostAsJsonAsync(StaffAuthRoutes.AcceptInvite, new AcceptStaffInviteRequest(Phone, code, Password));
        var accepted = await accept.Content.ReadFromJsonAsync<AcceptStaffInviteResponse>();

        Assert.NotNull(accepted);
        Assert.Equal(TestIds.OrganizationId, accepted.SignIn.OrganizationId);
        using var fresh = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/organizations/{TestIds.OrganizationId:D}/account/phone");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accepted.SignIn.AccessToken);
        var phone = await fresh.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, phone.StatusCode);
    }

    /// <summary>
    /// Панель в браузере не знает клуба заранее: приглашённый входит по номеру через корневой
    /// маршрут, и клуб выводится из номера. Логин приглашённого — не номер, поэтому вход «по
    /// логину» его номер не находил.
    /// </summary>
    [Fact]
    public async Task InvitedStaff_SignsInByPhoneWithoutNamingTheClub()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        await client.PostAsJsonAsync(
            StaffAuthRoutes.AcceptInvite, new AcceptStaffInviteRequest(Phone, await ReadCodeFromSmsAsync(factory), Password));

        using var fresh = factory.CreateClient();
        var signIn = await fresh.PostAsJsonAsync(StaffAuthRoutes.SignInByPhone, new StaffSignInByPhoneRequest("+992 93 738 00 70", Password));

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var session = await signIn.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.Equal(TestIds.OrganizationId, session!.OrganizationId);
    }

    /// <summary>
    /// Кого добавили, но кто не входил, виден руководителю — до первого входа. Без списка выданный
    /// код нельзя было ни увидеть, ни отозвать.
    /// </summary>
    [Fact]
    public async Task APendingInvite_IsListedUntilTheFirstSignIn()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);

        var pending = (await client.GetFromJsonAsync<List<StaffInviteSummaryDto>>(InvitesRoute))!.Single();
        Assert.Equal(StaffInviteStatusNames.Pending, pending.Status);
        Assert.Equal(3, pending.AttemptsLeft);
        Assert.Equal([OrganizationRoleNames.Operator], pending.RoleNames);

        await client.PostAsJsonAsync(StaffAuthRoutes.AcceptInvite, new AcceptStaffInviteRequest(Phone, await ReadCodeFromSmsAsync(factory), Password));

        Assert.Empty((await client.GetFromJsonAsync<List<StaffInviteSummaryDto>>(InvitesRoute))!);
    }

    [Fact]
    public async Task TheList_SaysWhenACodeIsSpent()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await client.PostAsJsonAsync(StaffAuthRoutes.CheckInvite, new CheckStaffInviteRequest(Phone, "000000"));
        }

        var pending = (await client.GetFromJsonAsync<List<StaffInviteSummaryDto>>(InvitesRoute))!.Single();
        Assert.Equal(StaffInviteStatusNames.Exhausted, pending.Status);
        Assert.Equal(0, pending.AttemptsLeft);
    }

    [Fact]
    public async Task ARevokedCode_NoLongerLetsAnyoneIn_AndLeavesATrace()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);
        var code = await ReadCodeFromSmsAsync(factory);
        var invite = (await client.GetFromJsonAsync<List<StaffInviteSummaryDto>>(InvitesRoute))!.Single();

        var revoked = await client.DeleteAsync($"{InvitesRoute}/{invite.StaffInviteId:D}");

        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(StaffSignInStepNames.Unknown, await NextStepAsync(client, Phone));
        Assert.NotEqual(HttpStatusCode.OK,
            (await client.PostAsJsonAsync(StaffAuthRoutes.AcceptInvite, new AcceptStaffInviteRequest(Phone, code, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{InvitesRoute}/{invite.StaffInviteId:D}")).StatusCode);
        Assert.True(await AuditedAsync(factory, AuditActionNames.RevokeStaffInvite, AuditOutcome.Succeeded));
    }

    /// <summary>Первый вход и подбор кода видны в журнале клуба.</summary>
    [Fact]
    public async Task TheFirstSignIn_AndAWrongCode_AreBothInTheAuditLog()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await InviteAsync(client);

        await client.PostAsJsonAsync(StaffAuthRoutes.CheckInvite, new CheckStaffInviteRequest(Phone, "000000"));
        Assert.True(await AuditedAsync(factory, AuditActionNames.AcceptStaffInvite, AuditOutcome.Denied));

        await client.PostAsJsonAsync(StaffAuthRoutes.AcceptInvite, new AcceptStaffInviteRequest(Phone, await ReadCodeFromSmsAsync(factory), Password));
        Assert.True(await AuditedAsync(factory, AuditActionNames.AcceptStaffInvite, AuditOutcome.Succeeded));
    }

    [Fact]
    public async Task ATechnician_CannotSeeOrRevokeCodes()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(InvitesRoute)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{InvitesRoute}/{Guid.NewGuid():D}")).StatusCode);
    }

    private static string InvitesRoute =>
        $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/staff/invites";

    private static async Task<bool> AuditedAsync(PlatformApiFactory factory, string action, string outcome)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.AuditRecords.AnyAsync(record => record.Action == action && record.Outcome == outcome);
    }

    private static async Task<string> NextStepAsync(HttpClient client, string phone)
    {
        var response = await client.PostAsJsonAsync(StaffAuthRoutes.NextStep, new StaffSignInNextStepRequest(phone));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StaffSignInNextStepResponse>())!.Step;
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
