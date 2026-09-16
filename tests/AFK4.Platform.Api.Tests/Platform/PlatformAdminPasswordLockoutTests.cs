using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

/// <summary>
/// Перебор пароля к платформенной панели.
///
/// Второй фактор запирался после пяти попыток, а пароль — нет: это была единственная дверь во
/// всей системе, которую можно было подбирать сколько угодно. За ней заведение клубов, деньги и
/// права всех сотрудников сети.
/// </summary>
public sealed class PlatformAdminPasswordLockoutTests
{
    [Fact]
    public async Task SignIn_AfterFiveWrongPasswords_LocksTheAccount()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(client, "wrong")).StatusCode);
        }

        var fifth = await SignInAsync(client, "wrong");

        Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
    }

    // Запертую учётную запись не открывает и верный пароль: иначе запрет обходится тем, кто его и
    // вызвал, — подбором до попадания.
    [Fact]
    public async Task SignIn_WhileLockedOut_RefusesEvenTheRightPassword()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await SignInAsync(client, "wrong");
        }

        var correct = await SignInAsync(client, PlatformAdminTestHelper.DefaultPassword);

        Assert.Equal(HttpStatusCode.TooManyRequests, correct.StatusCode);
    }

    // Пять промахов за год не должны однажды запереть того, кто каждый раз заходил успешно.
    [Fact]
    public async Task SignIn_WithTheRightPassword_ForgetsEarlierMisses()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory);

        await SignInAsync(client, "wrong");
        await SignInAsync(client, "wrong");
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, PlatformAdminTestHelper.DefaultPassword)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var admin = await dbContext.PlatformAdminUsers.SingleAsync();

        Assert.Equal(0, admin.FailedPasswordAttempts);
        Assert.Null(admin.PasswordLockedUntilUtc);
    }

    // Несуществующий логин отвечает тем же «неверно», что и неверный пароль: иначе перебор
    // сначала находит живые логины, а уже потом подбирает к ним пароль.
    [Fact]
    public async Task SignIn_WithAnUnknownUserName_AnswersLikeAWrongPassword()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/sign-in",
            new PlatformAdminSignInRequest("nobody@platform.test", "whatever"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string password) =>
        client.PostAsJsonAsync(
            "/api/platform/auth/sign-in",
            new PlatformAdminSignInRequest(PlatformAdminTestHelper.DefaultUserName, password));
}
