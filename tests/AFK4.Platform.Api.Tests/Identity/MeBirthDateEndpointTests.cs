using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Players;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Identity;

/// <summary>
/// День рождения в профиле — по желанию (владелец, 2026-09-26): для подарка клуба и игр с возрастом.
/// </summary>
public sealed class MeBirthDateEndpointTests
{
    // У человека ни одного клуба: день рождения — его, а не клубный, и клуб для него не нужен.
    [Fact]
    public async Task ThePersonSetsTheBirthday_AndSeesItInTheProfile()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000901");
        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId);

        var response = await client.PutAsJsonAsync("/api/me/birth-date", new SetBirthDateRequest(new DateOnly(2001, 3, 14)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new DateOnly(2001, 3, 14), (await response.Content.ReadFromJsonAsync<MePersonDto>())!.BirthDate);
        Assert.Equal(new DateOnly(2001, 3, 14), (await client.GetFromJsonAsync<MeDto>("/api/me"))!.Person.BirthDate);
        Assert.NotNull((await ReadPersonAsync(factory, person.PlatformPersonId)).BirthDateSetAtUtc);
    }

    // Та же дата ещё раз — не смена: иначе повторное сохранение профиля сбрасывало бы срок
    // «введена заранее» и отнимало подарок у честного игрока.
    [Fact]
    public async Task TheSameDateAgain_KeepsWhenItWasEntered()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000902");
        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId);

        var set = await client.PutAsJsonAsync("/api/me/birth-date", new SetBirthDateRequest(new DateOnly(1999, 12, 1)));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var first = (await ReadPersonAsync(factory, person.PlatformPersonId)).BirthDateSetAtUtc;
        Assert.NotNull(first);
        var again = await client.PutAsJsonAsync("/api/me/birth-date", new SetBirthDateRequest(new DateOnly(1999, 12, 1)));

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(first, (await ReadPersonAsync(factory, person.PlatformPersonId)).BirthDateSetAtUtc);
    }

    [Fact]
    public async Task EmptyDate_RemovesTheBirthday()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000903");
        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId);
        await client.PutAsJsonAsync("/api/me/birth-date", new SetBirthDateRequest(new DateOnly(2000, 1, 1)));

        var response = await client.PutAsJsonAsync("/api/me/birth-date", new SetBirthDateRequest(null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await ReadPersonAsync(factory, person.PlatformPersonId);
        Assert.Null(stored.BirthDate);
        Assert.Null(stored.BirthDateSetAtUtc);
    }

    [Fact]
    public async Task ADateInTheFuture_OrATooYoungAge_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000904");
        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var future = await client.PutAsJsonAsync("/api/me/birth-date", new SetBirthDateRequest(today.AddDays(10)));
        var baby = await client.PutAsJsonAsync("/api/me/birth-date", new SetBirthDateRequest(today.AddYears(-2)));

        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Contains("invalid_birth_date", await future.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, baby.StatusCode);
        Assert.Null((await ReadPersonAsync(factory, person.PlatformPersonId)).BirthDate);
    }

    [Fact]
    public async Task WithoutSigningIn_NothingChanges()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/me/birth-date", new SetBirthDateRequest(new DateOnly(2000, 1, 1)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("2000-03-14", "2026-03-13", 25)]
    [InlineData("2000-03-14", "2026-03-14", 26)]
    // Родившийся 29 февраля в невисокосный год взрослеет 28-го — в тот же день, когда его поздравляют.
    [InlineData("2008-02-29", "2026-02-28", 18)]
    [InlineData("2008-02-29", "2026-02-27", 17)]
    public void Age_CountsFullYears_OnTheClubsCalendar(string birth, string day, int age)
    {
        Assert.Equal(age, PlayerBirthdays.AgeOn(DateOnly.Parse(birth), DateOnly.Parse(day)));
    }

    [Fact]
    public void TheLeapDayBirthday_IsCelebratedOnTheTwentyEighth_InOrdinaryYears()
    {
        Assert.Equal(new DateOnly(2026, 2, 28), PlayerBirthdays.BirthdayIn(2026, new DateOnly(2008, 2, 29)));
        Assert.Equal(new DateOnly(2028, 2, 29), PlayerBirthdays.BirthdayIn(2028, new DateOnly(2008, 2, 29)));
    }

    private static async Task AuthorizeAsync(PlatformApiFactory factory, HttpClient client, Guid platformPersonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<IPlatformPersonTokenService>();
        var person = await db.PlatformPersons.SingleAsync(candidate => candidate.PlatformPersonId == platformPersonId);
        var session = await tokens.IssueAsync(person, null, CancellationToken.None);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private static async Task<PlatformPersonEntity> ReadPersonAsync(PlatformApiFactory factory, Guid platformPersonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.PlatformPersons.AsNoTracking().SingleAsync(candidate => candidate.PlatformPersonId == platformPersonId);
    }
}
