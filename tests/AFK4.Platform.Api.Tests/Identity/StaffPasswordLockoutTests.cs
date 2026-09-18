using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Identity;

/// <summary>
/// Перебор пароля сотрудника.
///
/// Частота ограничена по адресу — десять попыток в минуту, — но адресов у перебирающего столько,
/// сколько он купит. Счёт промахов живёт на самой учётной записи, поэтому смена адреса от него не
/// спасает. За этой дверью касса, клиенты и право открывать любой ПК клуба.
/// </summary>
public sealed class StaffPasswordLockoutTests
{
    private const string CorrectPassword = "246813";

    [Fact]
    public async Task SignIn_AfterFiveWrongPasswords_LocksTheAccount()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        // Учётную запись заводит общий помощник (он же и подписывает первый вход); нам нужна
        // только она сама, поэтому заголовок авторизации дальше не используется.
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedStaffAsync(factory);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(client, "wrong")).StatusCode);
        }

        var fifth = await SignInAsync(client, "wrong");

        Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
        Assert.Equal(StaffAuthErrorCodeNames.TooManyPasswordAttempts, await ReadCodeAsync(fifth));
    }

    // Запертую учётную запись не открывает и верный пароль: иначе запрет обходит тот, кто его и
    // вызвал, — подбором до попадания.
    [Fact]
    public async Task SignIn_WhileLockedOut_RefusesEvenTheRightPassword()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        // Учётную запись заводит общий помощник (он же и подписывает первый вход); нам нужна
        // только она сама, поэтому заголовок авторизации дальше не используется.
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedStaffAsync(factory);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await SignInAsync(client, "wrong");
        }

        var correct = await SignInAsync(client, CorrectPassword);

        Assert.Equal(HttpStatusCode.TooManyRequests, correct.StatusCode);
    }

    // Пять промахов за год не должны однажды запереть того, кто каждый раз заходил успешно.
    [Fact]
    public async Task SignIn_WithTheRightPassword_ForgetsEarlierMisses()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        // Учётную запись заводит общий помощник (он же и подписывает первый вход); нам нужна
        // только она сама, поэтому заголовок авторизации дальше не используется.
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedStaffAsync(factory);

        await SignInAsync(client, "wrong");
        await SignInAsync(client, "wrong");
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, CorrectPassword)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staff = await dbContext.StaffUsers.SingleAsync(user => user.StaffUserId == TestIds.TechnicianStaffUserId);

        Assert.Equal(0, staff.FailedPasswordAttempts);
        Assert.Null(staff.PasswordLockedUntilUtc);
    }

    // Мастер установки входит по телефону и по логину, не называя организацию. Через эти двери
    // перебор точно так же не должен быть бесконечным.
    [Fact]
    public async Task SignInByPhone_AfterFiveWrongPasswords_LocksTheAccount()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        // Учётную запись заводит общий помощник (он же и подписывает первый вход); нам нужна
        // только она сама, поэтому заголовок авторизации дальше не используется.
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedStaffAsync(factory);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await client.PostAsJsonAsync(
                StaffAuthRoutes.SignInByPhone,
                new StaffSignInByPhoneRequest("992900000777", "wrong"));
        }

        var correct = await client.PostAsJsonAsync(
            StaffAuthRoutes.SignInByPhone,
            new StaffSignInByPhoneRequest("992900000777", CorrectPassword));

        Assert.Equal(HttpStatusCode.TooManyRequests, correct.StatusCode);
    }

    [Fact]
    public async Task SignInByLogin_AfterFiveWrongPasswords_LocksTheAccount()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        // Учётную запись заводит общий помощник (он же и подписывает первый вход); нам нужна
        // только она сама, поэтому заголовок авторизации дальше не используется.
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedStaffAsync(factory);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await client.PostAsJsonAsync(
                StaffAuthRoutes.SignInByLogin,
                new StaffSignInByLoginRequest("tech@afk4.test", "wrong"));
        }

        var correct = await client.PostAsJsonAsync(
            StaffAuthRoutes.SignInByLogin,
            new StaffSignInByLoginRequest("tech@afk4.test", CorrectPassword));

        Assert.Equal(HttpStatusCode.TooManyRequests, correct.StatusCode);
        Assert.Equal(StaffAuthErrorCodeNames.TooManyPasswordAttempts, await ReadCodeAsync(correct));
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string password) =>
        client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "tech@afk4.test", password));

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
            ? code.GetString()
            : null;
    }

    private static async Task SeedStaffAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staff = await dbContext.StaffUsers.SingleAsync(
            user => user.StaffUserId == TestIds.TechnicianStaffUserId);

        // Вход по телефону требует подтверждённого номера — иначе эта дверь просто не откроется.
        staff.Phone = "+992900000777";
        staff.NormalizedPhone = PhoneNumberNormalizer.Normalize("+992900000777");
        staff.PhoneVerifiedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z");
        await dbContext.SaveChangesAsync();
    }
}
