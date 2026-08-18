using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Tests.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Data;

/// <summary>
/// Форма новой части схемы: личность, её связь с клубными счетами и обещания, которые эта связь
/// даёт. Уникальность здесь — не украшение модели, а единственное, что мешает завести человеку
/// второй счёт в том же клубе, поэтому она проверяется на настоящей PostgreSQL: in-memory провайдер
/// уникальные индексы не исполняет и покажет зелёное на данных, которые база не примет.
/// </summary>
public sealed class PlatformPersonSchemaTests
{
    [Fact]
    public void Person_IsKeyedByPhone_AndCarriesNetworkPinAndBan()
    {
        var entityType = Model().FindEntityType(typeof(PlatformPersonEntity));

        Assert.NotNull(entityType);
        Assert.Equal("platform_persons", entityType!.GetTableName());

        var unique = Assert.Single(entityType.GetIndexes(), index => index.IsUnique);
        Assert.Equal(
            new[] { nameof(PlatformPersonEntity.PhoneNumber) },
            unique.Properties.Select(property => property.Name).ToArray());

        Assert.False(entityType.FindProperty(nameof(PlatformPersonEntity.PhoneNumber))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(PlatformPersonEntity.PinHash))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(PlatformPersonEntity.NetworkBanAtUtc))!.IsNullable);
    }

    [Fact]
    public void PlayerAccount_KeepsOneAccountPerPersonPerClub_WithoutForbiddingPhonelessGuests()
    {
        var entityType = Model().FindEntityType(typeof(PlayerAccountEntity))!;

        // Гость без телефона — нормальный житель системы, поэтому связь необязательна.
        Assert.True(entityType.FindProperty(nameof(PlayerAccountEntity.PlatformPersonId))!.IsNullable);

        var membership = Assert.Single(
            entityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(PlayerAccountEntity.PlatformPersonId), nameof(PlayerAccountEntity.OrganizationId) }));
        Assert.True(membership.IsUnique);
        // Без фильтра ограничение запретило бы второго безымянного гостя в том же клубе.
        Assert.Contains("PlatformPersonId", membership.GetFilter());
    }

    [Fact]
    public void PlatformPhoneOtp_IsKeyedByPhone_BecauseAStrangerHasNoAccountYet()
    {
        var entityType = Model().FindEntityType(typeof(PlatformPhoneOtpEntity))!;

        Assert.Equal("platform_phone_otps", entityType.GetTableName());
        Assert.Contains(
            entityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual(
                new[]
                {
                    nameof(PlatformPhoneOtpEntity.Phone),
                    nameof(PlatformPhoneOtpEntity.Purpose),
                    nameof(PlatformPhoneOtpEntity.CreatedAtUtc)
                }));
    }

    [Fact]
    public void ReservationCarriesRespondByAndConfirmedAt()
    {
        var entityType = Model().FindEntityType(typeof(ReservationEntity))!;

        Assert.True(entityType.FindProperty(nameof(ReservationEntity.RespondByUtc))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(ReservationEntity.ConfirmedAtUtc))!.IsNullable);
    }

    [Fact]
    public void BranchBookingSettings_AreOnePerBranch()
    {
        var entityType = Model().FindEntityType(typeof(BranchBookingSettingsEntity))!;

        Assert.Equal("branch_booking_settings", entityType.GetTableName());
        Assert.Equal(
            new[] { nameof(BranchBookingSettingsEntity.BranchId) },
            entityType.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());
    }

    [PostgresSessionFact]
    public async Task Person_RoundTripsAndRefusesASecondRecordForTheSameNumber()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);

        var now = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        var personId = Guid.NewGuid();
        await using (var db = database.CreateDbContext())
        {
            db.PlatformPersons.Add(new PlatformPersonEntity
            {
                PlatformPersonId = personId,
                PhoneNumber = "+992900000001",
                DisplayName = "Фаррух",
                PreferredLocale = "tg",
                PhoneVerifiedAtUtc = now,
                NetworkBanAtUtc = now,
                NetworkBanReason = "Сетевой запрет",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext())
        {
            var stored = await db.PlatformPersons.AsNoTracking().SingleAsync();
            Assert.Equal("+992900000001", stored.PhoneNumber);
            Assert.Equal("Фаррух", stored.DisplayName);
            Assert.Equal("tg", stored.PreferredLocale);
            Assert.Equal(now, stored.PhoneVerifiedAtUtc);
            Assert.Equal("Сетевой запрет", stored.NetworkBanReason);
            Assert.Null(stored.PinHash);
            Assert.True(stored.IsActive);
        }

        await using (var db = database.CreateDbContext())
        {
            db.PlatformPersons.Add(new PlatformPersonEntity
            {
                PlatformPersonId = Guid.NewGuid(),
                PhoneNumber = "+992900000001",
                DisplayName = "Кто-то ещё",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [PostgresSessionFact]
    public async Task Club_HoldsOneAccountPerPerson_ButAnyNumberOfPhonelessGuests()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);

        var now = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        var personId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        await using (var db = database.CreateDbContext())
        {
            db.PlatformPersons.Add(new PlatformPersonEntity
            {
                PlatformPersonId = personId,
                PhoneNumber = "+992900000002",
                DisplayName = "Фаррух",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            db.PlayerAccounts.Add(NewAccount(organizationId, branchId, personId, now));
            db.PlayerAccounts.Add(NewAccount(organizationId, branchId, null, now));
            db.PlayerAccounts.Add(NewAccount(organizationId, branchId, null, now));
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext())
        {
            // Тот же человек в другом клубе — это нормально и есть вся суть модели.
            db.PlayerAccounts.Add(NewAccount(Guid.NewGuid(), Guid.NewGuid(), personId, now));
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext())
        {
            db.PlayerAccounts.Add(NewAccount(organizationId, branchId, personId, now));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    private static PlayerAccountEntity NewAccount(
        Guid organizationId,
        Guid branchId,
        Guid? platformPersonId,
        DateTimeOffset now) => new()
        {
            PlayerAccountId = Guid.NewGuid(),
            OrganizationId = organizationId,
            HomeBranchId = branchId,
            PlatformPersonId = platformPersonId,
            DisplayName = "Гость",
            IsActive = true,
            CreatedAtUtc = now
        };

    private static Microsoft.EntityFrameworkCore.Metadata.IModel Model()
    {
        using var factory = new PlatformApiFactory();
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Model;
    }
}
