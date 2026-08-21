using AFK4.Platform.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Игрок, который может сесть за ПК сам. С тех пор как PIN принадлежит личности, а не клубному
/// счёту, «завести игрока с PIN» — это завести человека и подшить к нему карточку клуба; клубный
/// <see cref="PlayerCredentialEntity"/> больше не участвует во входе ни одной стороной.
/// </summary>
internal static class PlayerPinTestData
{
    public const string DefaultPin = "1234";

    /// <summary>
    /// Заводит личность с сетевым PIN и подшивает к ней существующую клубную карточку. Телефон
    /// берётся из самой карточки, если он там есть, — так тест продолжает входить тем же номером,
    /// каким входил раньше.
    /// </summary>
    public static async Task<Guid> AttachPersonWithPinAsync(
        PlatformApiFactory factory,
        Guid playerAccountId,
        string? phoneNumber = null,
        string pin = DefaultPin,
        bool phoneVerified = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var account = await db.PlayerAccounts.SingleAsync(
            candidate => candidate.PlayerAccountId == playerAccountId);

        var phone = phoneNumber ?? account.PhoneNumber;
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        var now = account.CreatedAtUtc;
        var person = new PlatformPersonEntity
        {
            PlatformPersonId = Guid.NewGuid(),
            PhoneNumber = phone,
            DisplayName = account.DisplayName,
            PreferredLocale = account.PreferredLocale,
            PhoneVerifiedAtUtc = phoneVerified ? now : null,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            PinSetAtUtc = now
        };
        person.PinHash = new PasswordHasher<PlatformPersonEntity>().HashPassword(person, pin);
        db.PlatformPersons.Add(person);

        account.PlatformPersonId = person.PlatformPersonId;
        account.PhoneNumber = phone;
        await db.SaveChangesAsync();

        return person.PlatformPersonId;
    }
}
