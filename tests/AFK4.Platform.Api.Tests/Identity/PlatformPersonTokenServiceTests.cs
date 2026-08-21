using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Identity;

/// <summary>
/// Токены личности. Отличие от токенов клубного счёта одно и оно важное: закрывает вход человеку
/// сама личность, а не клуб. Клуб, закрывший у себя карточку, не должен закрывать человеку вход
/// в соседние клубы.
/// </summary>
public sealed class PlatformPersonTokenServiceTests
{
    [Fact]
    public async Task IssuedToken_NamesThePersonAndRemembersTheClubChosenAtSignIn()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000101");
        var account = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);

        var tokens = await IssueAsync(factory, person.PlatformPersonId, account.PlayerAccountId);

        // Тело ответа осталось прежним: клиент читает те же поля, что и до перехода.
        Assert.Equal(account.PlayerAccountId, tokens.PlayerAccountId);
        Assert.Equal(account.OrganizationId, tokens.OrganizationId);
        Assert.Equal("Фаррух", tokens.DisplayName);
        Assert.True(tokens.PhoneVerified);

        var context = await ValidateAsync(factory, tokens.AccessToken);
        Assert.NotNull(context);
        Assert.Equal(person.PlatformPersonId, context!.PlatformPersonId);
        Assert.Equal(account.OrganizationId, context.PinnedOrganizationId);
    }

    [Fact]
    public async Task RefreshToken_WorksOnceAndComesBackRotated()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000102");
        var account = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        var issued = await IssueAsync(factory, person.PlatformPersonId, account.PlayerAccountId);

        var refreshed = await RefreshAsync(factory, issued.RefreshToken);
        Assert.NotNull(refreshed);
        Assert.NotEqual(issued.RefreshToken, refreshed!.RefreshToken);
        Assert.NotEqual(issued.AccessToken, refreshed.AccessToken);
        Assert.NotNull(await ValidateAsync(factory, refreshed.AccessToken));

        // Второй раз тот же refresh не работает: перехваченный токен стоит одной попытки.
        Assert.Null(await RefreshAsync(factory, issued.RefreshToken));
    }

    [Fact]
    public async Task DeactivatedPerson_LosesEveryTokenAtOnce()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000103");
        var account = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        var tokens = await IssueAsync(factory, person.PlatformPersonId, account.PlayerAccountId);
        Assert.NotNull(await ValidateAsync(factory, tokens.AccessToken));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var stored = await db.PlatformPersons.SingleAsync(
                candidate => candidate.PlatformPersonId == person.PlatformPersonId);
            stored.IsActive = false;
            await db.SaveChangesAsync();
        }

        Assert.Null(await ValidateAsync(factory, tokens.AccessToken));
        Assert.Null(await RefreshAsync(factory, tokens.RefreshToken));
    }

    [Fact]
    public async Task DeactivatedClubCard_DoesNotCloseTheDoorToOtherClubs()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000104");
        var account = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        var tokens = await IssueAsync(factory, person.PlatformPersonId, account.PlayerAccountId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var stored = await db.PlayerAccounts.SingleAsync(
                candidate => candidate.PlayerAccountId == account.PlayerAccountId);
            stored.IsActive = false;
            await db.SaveChangesAsync();
        }

        // Личность жива — значит вход жив. Клуба у запроса при этом уже нет, и это разные вопросы.
        Assert.NotNull(await ValidateAsync(factory, tokens.AccessToken));
    }

    [Fact]
    public async Task TamperedOrUnknownToken_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000105");
        var account = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        var tokens = await IssueAsync(factory, person.PlatformPersonId, account.PlayerAccountId);

        var identifier = tokens.AccessToken.Split('.')[0];
        Assert.Null(await ValidateAsync(factory, $"{identifier}.deadbeef"));
        Assert.Null(await ValidateAsync(factory, $"{Guid.NewGuid():N}.deadbeef"));
        Assert.Null(await ValidateAsync(factory, "not-a-token"));
        Assert.Null(await ValidateAsync(factory, null));
    }

    private static async Task<Shared.Contracts.Identity.PlatformPersonSessionResponse> IssueAsync(
        PlatformApiFactory factory, Guid platformPersonId, Guid playerAccountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformPersonTokenService>();
        var person = await db.PlatformPersons.SingleAsync(
            candidate => candidate.PlatformPersonId == platformPersonId);
        var account = await db.PlayerAccounts.SingleAsync(
            candidate => candidate.PlayerAccountId == playerAccountId);
        return await service.IssueAsync(person, account, CancellationToken.None);
    }

    private static async Task<PlatformPersonContext?> ValidateAsync(PlatformApiFactory factory, string? token)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPlatformPersonTokenService>()
            .ValidateAsync(token, CancellationToken.None);
    }

    private static async Task<Shared.Contracts.Identity.PlatformPersonSessionResponse?> RefreshAsync(
        PlatformApiFactory factory, string token)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPlatformPersonTokenService>()
            .RefreshAsync(token, CancellationToken.None);
    }
}
