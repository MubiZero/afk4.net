using AFK4.Platform.Api.Data;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Identity;

/// <summary>Человек, его клубы и их счета — общая заготовка для тестов личности.</summary>
internal static class PlatformPersonTestData
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-19T09:00:00Z");

    public static async Task<PlatformPersonEntity> AddPersonAsync(
        PlatformApiFactory factory,
        string phoneNumber,
        string displayName = "Фаррух",
        bool phoneVerified = true,
        bool isActive = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var person = new PlatformPersonEntity
        {
            PlatformPersonId = Guid.NewGuid(),
            PhoneNumber = phoneNumber,
            DisplayName = displayName,
            PreferredLocale = "tg",
            PhoneVerifiedAtUtc = phoneVerified ? Now : null,
            IsActive = isActive,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        db.PlatformPersons.Add(person);
        await db.SaveChangesAsync();
        return person;
    }

    public static async Task<PlayerAccountEntity> AddClubAsync(
        PlatformApiFactory factory,
        Guid? platformPersonId,
        string organizationName = "Клуб")
    {
        var club = await AddClubWithoutAccountsAsync(factory, organizationName);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var account = new PlayerAccountEntity
        {
            PlayerAccountId = Guid.NewGuid(),
            OrganizationId = club.OrganizationId,
            PlatformPersonId = platformPersonId,
            HomeBranchId = club.BranchId,
            DisplayName = "Карточка клуба",
            IsActive = true,
            CreatedAtUtc = Now
        };
        db.PlayerAccounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    /// <summary>Клуб с одним филиалом и без единого счёта: сюда человек приходит впервые.</summary>
    public static async Task<(Guid OrganizationId, Guid BranchId)> AddClubWithoutAccountsAsync(
        PlatformApiFactory factory,
        string organizationName = "Клуб")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Name = organizationName,
            CreatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Name = organizationName + " — филиал",
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return (organizationId, branchId);
    }

    public static async Task AddLedgerEntryAsync(
        PlatformApiFactory factory,
        PlayerAccountEntity account,
        string entryType,
        string accountType,
        long amountMinorUnits,
        Guid? reversesLedgerEntryId = null,
        Guid? ledgerEntryId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = ledgerEntryId ?? Guid.NewGuid(),
            OrganizationId = account.OrganizationId,
            BranchId = account.HomeBranchId,
            PlayerAccountId = account.PlayerAccountId,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = entryType,
            ReversesLedgerEntryId = reversesLedgerEntryId,
            CreatedByStaffUserId = Guid.Empty,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }
}
