using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Updates;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var options = DevSeedOptions.Parse(args);

var dbOptions = new DbContextOptionsBuilder<PlatformDbContext>()
    .UseNpgsql(options.ConnectionString)
    .Options;

await using var dbContext = new PlatformDbContext(dbOptions);

if (options.ResetDatabase)
{
    Console.WriteLine("Dropping local AFK4 database...");
    await dbContext.Database.EnsureDeletedAsync();
}

Console.WriteLine("Applying EF Core migrations...");
await dbContext.Database.MigrateAsync();

var seed = new LocalDevSeed(dbContext, options.OperatorPassword);
await seed.SeedAsync();

Console.WriteLine("Local AFK4 dev database is ready.");
Console.WriteLine($"Organization: {LocalDevSeed.OrganizationId}");
Console.WriteLine($"Branch:       {LocalDevSeed.BranchId}");
Console.WriteLine($"Login:        {LocalDevSeed.OwnerUserName}");
Console.WriteLine($"Password:     {options.OperatorPassword}");

internal sealed record DevSeedOptions(string ConnectionString, string OperatorPassword, bool ResetDatabase)
{
    private const string DefaultConnectionString = "Host=localhost;Port=5432;Database=afk4_dev;Username=postgres";
    private const string DefaultOperatorPassword = "Passw0rd!";

    public static DevSeedOptions Parse(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PlatformDatabase")
            ?? DefaultConnectionString;
        var password = Environment.GetEnvironmentVariable("AFK4_DEV_SEED_OPERATOR_PASSWORD")
            ?? DefaultOperatorPassword;
        var reset = true;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--connection-string" when index + 1 < args.Length:
                    connectionString = args[++index];
                    break;
                case "--password" when index + 1 < args.Length:
                    password = args[++index];
                    break;
                case "--no-reset":
                    reset = false;
                    break;
                case "--reset":
                    reset = true;
                    break;
            }
        }

        return new DevSeedOptions(connectionString, password, reset);
    }
}

internal sealed class LocalDevSeed(PlatformDbContext dbContext, string operatorPassword)
{
    public static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    public static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    public static readonly Guid OwnerStaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");
    public static readonly Guid PlatformAdminUserId = Guid.Parse("c10e7d55-2dd8-43e4-970a-6d59a90c6f33");
    public const string OwnerUserName = "owner@afk4.test";

    private const string CurrencyCode = "TJS";

    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    public async Task SeedAsync()
    {
        if (await dbContext.Organizations.AnyAsync(organization => organization.OrganizationId == OrganizationId))
        {
            Console.WriteLine("Seed data already exists. Use --reset to rebuild it.");
            return;
        }

        SeedPlatformAdmin();
        var staff = SeedOrganizationAndStaff();
        var zones = SeedLayout();
        var seats = zones.SelectMany(zone => zone.Seats).ToList();
        var devices = SeedDevices(seats);
        var players = SeedPlayers(staff.OrganizationOwner.StaffUserId);
        var billing = SeedBillingCatalog();
        var shift = SeedOpenShift(staff.OrganizationOwner.StaffUserId);

        SeedSessions(staff.OrganizationOwner.StaffUserId, seats, devices, players, billing.StandardTariffVersionId);
        SeedLedger(staff.OrganizationOwner.StaffUserId, shift.ShiftId, players);
        SeedPos(staff.OrganizationOwner.StaffUserId, shift.ShiftId, players.PrimaryPlayerId);
        SeedReservations(staff.OrganizationOwner.StaffUserId, seats, players);
        SeedUpdates(PlatformAdminUserId, devices);
        SeedAudit(staff.OrganizationOwner.StaffUserId);

        await dbContext.SaveChangesAsync();
    }

    private void SeedPlatformAdmin()
    {
        var admin = new PlatformAdminUserEntity
        {
            PlatformAdminUserId = PlatformAdminUserId,
            UserName = "platform@afk4.test",
            NormalizedUserName = "PLATFORM@AFK4.TEST",
            DisplayName = "AFK4 Platform Admin",
            RolesJson = JsonSerializer.Serialize(new[] { PlatformAdminRoleNames.PlatformAdmin }),
            IsActive = true,
            CreatedAtUtc = now.AddDays(-10),
            UpdatedAtUtc = now.AddDays(-10)
        };
        admin.PasswordHash = new PasswordHasher<PlatformAdminUserEntity>().HashPassword(admin, operatorPassword);
        dbContext.PlatformAdminUsers.Add(admin);
    }

    private StaffSeed SeedOrganizationAndStaff()
    {
        dbContext.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = OrganizationId,
            Slug = "demo",
            Name = "AFK4 Demo",
            LogoUrl = null,
            AccentColor = "#c8ff00",
            CreatedAtUtc = now.AddDays(-10)
        });

        dbContext.Branches.Add(new BranchEntity
        {
            BranchId = BranchId,
            OrganizationId = OrganizationId,
            Name = "AFK4 Душанбе",
            City = "Душанбе",
            CreatedAtUtc = now.AddDays(-10)
        });

        var owner = CreateStaff(OwnerStaffUserId, OwnerUserName, "Администратор демо", OrganizationRoleNames.OrganizationOwner);
        var cashier = CreateStaff(StableGuid(6102), "operator@afk4.test", "Оператор смены", OrganizationRoleNames.Operator);
        var technician = CreateStaff(StableGuid(6103), "tech@afk4.test", "Техник зала", OrganizationRoleNames.Technician);

        return new StaffSeed(owner, cashier, technician);
    }

    private StaffUserEntity CreateStaff(Guid staffUserId, string userName, string displayName, params string[] roleNames)
    {
        var staffUser = new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = OrganizationId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = displayName,
            IsActive = true,
            CreatedAtUtc = now.AddDays(-10)
        };

        staffUser.PasswordHash = new PasswordHasher<StaffUserEntity>().HashPassword(staffUser, operatorPassword);
        dbContext.StaffUsers.Add(staffUser);

        var roleOffset = 0;
        foreach (var roleName in roleNames)
        {
            dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = StableGuid(6200 + Math.Abs(HashCode.Combine(staffUserId, roleName)) % 7000 + roleOffset++),
                StaffUserId = staffUserId,
                OrganizationId = OrganizationId,
                BranchId = BranchId,
                RoleName = roleName
            });
        }

        return staffUser;
    }

    private List<ZoneSeed> SeedLayout()
    {
        var zones = new List<ZoneSeed>
        {
            new(StableGuid(1001), "Основной зал", 10, 2100, ["PC-001", "PC-002", "PC-003", "PC-004", "PC-005", "PC-006", "PC-007", "PC-008", "PC-009", "PC-010", "PC-011", "PC-012"]),
            new(StableGuid(1002), "Консольная зона", 20, 2200, ["PS-01", "PS-02", "PS-03", "PS-04"]),
            new(StableGuid(1003), "VIP-зал", 30, 2300, ["VIP-01", "VIP-02", "VIP-03", "VIP-04", "VIP-05", "VIP-06"]),
            new(StableGuid(1004), "Буткемп", 40, 2400, ["BOOT-01", "BOOT-02", "BOOT-03", "BOOT-04"])
        };

        foreach (var zone in zones)
        {
            dbContext.Zones.Add(new ZoneEntity
            {
                ZoneId = zone.ZoneId,
                OrganizationId = OrganizationId,
                BranchId = BranchId,
                Name = zone.Name,
                SortOrder = zone.SortOrder,
                CreatedAtUtc = now.AddDays(-9)
            });

            var sortOrder = 10;
            foreach (var seat in zone.Seats)
            {
                seat.SortOrder = sortOrder;
                sortOrder += 10;
                dbContext.Seats.Add(new SeatEntity
                {
                    SeatId = seat.SeatId,
                    OrganizationId = OrganizationId,
                    BranchId = BranchId,
                    ZoneId = zone.ZoneId,
                    Name = seat.Name,
                    SortOrder = seat.SortOrder,
                    CreatedAtUtc = now.AddDays(-9)
                });
            }
        }

        return zones;
    }

    private Dictionary<Guid, DeviceEntity> SeedDevices(IReadOnlyCollection<SeatSeed> seats)
    {
        var devices = new Dictionary<Guid, DeviceEntity>();
        var unassignedSeatNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PC-012",
            "PS-03",
            "VIP-06"
        };
        var offlineSeatNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PC-004",
            "VIP-04"
        };
        var unlockedSeatNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PC-002",
            "PC-006",
            "PS-02",
            "BOOT-02"
        };

        foreach (var seat in seats)
        {
            if (unassignedSeatNames.Contains(seat.Name))
            {
                continue;
            }

            var deviceId = StableGuid(3000 + seat.Index);
            var isOffline = offlineSeatNames.Contains(seat.Name);
            var device = new DeviceEntity
            {
                DeviceId = deviceId,
                OrganizationId = OrganizationId,
                BranchId = BranchId,
                MachineName = $"AFK-{seat.Name}",
                AgentVersion = "0.1.14",
                ShellVersion = "0.1.14",
                EnrolledAtUtc = now.AddDays(-8),
                LastHeartbeatAtUtc = isOffline ? now.AddMinutes(-17) : now.AddSeconds(-40 - seat.Index),
                IsOnline = !isOffline,
                IsLocked = !unlockedSeatNames.Contains(seat.Name)
            };

            dbContext.Devices.Add(device);
            dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = StableGuid(4000 + seat.Index),
                OrganizationId = OrganizationId,
                BranchId = BranchId,
                SeatId = seat.SeatId,
                DeviceId = deviceId,
                AttachedAtUtc = now.AddDays(-8),
                DetachedAtUtc = null
            });

            dbContext.DeviceInstalledApps.AddRange(
                InstalledApp(StableGuid(410000 + seat.Index * 10 + 1), deviceId, "Counter-Strike 2", "2.0"),
                InstalledApp(StableGuid(410000 + seat.Index * 10 + 2), deviceId, "Dota 2", "7.36"),
                InstalledApp(StableGuid(410000 + seat.Index * 10 + 3), deviceId, "Steam", "2.10"));

            devices[seat.SeatId] = device;
        }

        return devices;
    }

    private DeviceInstalledAppEntity InstalledApp(Guid id, Guid deviceId, string name, string version) => new()
    {
        DeviceInstalledAppId = id,
        OrganizationId = OrganizationId,
        BranchId = BranchId,
        DeviceId = deviceId,
        DisplayName = name,
        Version = version,
        Publisher = "Valve",
        InstallLocation = "C:\\Games",
        InstalledAtUtc = now.AddDays(-7),
        ReportedAtUtc = now.AddMinutes(-15)
    };

    private PlayerSeed SeedPlayers(Guid staffUserId)
    {
        var players = new PlayerSeed(
            PrimaryPlayerId: StableGuid(7001),
            VipPlayerId: StableGuid(7002),
            DebtPlayerId: StableGuid(7003),
            GuestPlayerId: StableGuid(7004));

        dbContext.PlayerAccounts.AddRange(
            Player(players.PrimaryPlayerId, "Амир К.", "+992900000101"),
            Player(players.VipPlayerId, "Мадина С.", "+992900000202"),
            Player(players.DebtPlayerId, "Юсуф А.", "+992900000303"),
            Player(players.GuestPlayerId, "Гость зала", null));

        return players;
    }

    private PlayerAccountEntity Player(Guid playerAccountId, string displayName, string? phoneNumber) => new()
    {
        PlayerAccountId = playerAccountId,
        OrganizationId = OrganizationId,
        HomeBranchId = BranchId,
        DisplayName = displayName,
        PhoneNumber = phoneNumber,
        IsActive = true,
        CreatedAtUtc = now.AddDays(-6)
    };

    private BillingSeed SeedBillingCatalog()
    {
        var standardTariffId = StableGuid(8001);
        var vipTariffId = StableGuid(8002);
        var nightTariffId = StableGuid(8003);
        var standardVersionId = StableGuid(8101);

        dbContext.Tariffs.AddRange(
            Tariff(standardTariffId, "Стандарт", true),
            Tariff(vipTariffId, "VIP", true),
            Tariff(nightTariffId, "Ночной", true));

        dbContext.TariffVersions.AddRange(
            TariffVersion(standardVersionId, standardTariffId, 1, 100, 10, 5),
            TariffVersion(StableGuid(8102), vipTariffId, 1, 160, 10, 5),
            TariffVersion(StableGuid(8103), nightTariffId, 1, 70, 60, 15));

        dbContext.PackageDefinitions.AddRange(
            Package(StableGuid(8201), "3 часа", 28000, 3 * 3600, 15 * 60, 14),
            Package(StableGuid(8202), "10 часов", 82000, 10 * 3600, 60 * 60, 30),
            Package(StableGuid(8203), "Ночь 5 часов", 35000, 5 * 3600, 0, 7));

        return new BillingSeed(standardVersionId);
    }

    private TariffEntity Tariff(Guid tariffId, string name, bool isActive) => new()
    {
        TariffId = tariffId,
        OrganizationId = OrganizationId,
        BranchId = BranchId,
        Name = name,
        IsActive = isActive,
        CreatedAtUtc = now.AddDays(-6)
    };

    private TariffVersionEntity TariffVersion(Guid versionId, Guid tariffId, int version, long pricePerMinute, int minimumMinutes, int roundingMinutes) => new()
    {
        TariffVersionId = versionId,
        TariffId = tariffId,
        OrganizationId = OrganizationId,
        BranchId = BranchId,
        VersionNumber = version,
        CurrencyCode = CurrencyCode,
        PricePerMinuteMinorUnits = pricePerMinute,
        MinimumBillableMinutes = minimumMinutes,
        RoundingIncrementMinutes = roundingMinutes,
        EffectiveFromUtc = now.AddDays(-5),
        RetiredAtUtc = null,
        CreatedAtUtc = now.AddDays(-5)
    };

    private PackageDefinitionEntity Package(Guid packageDefinitionId, string name, long price, int includedSeconds, int bonusSeconds, int expiresAfterDays) => new()
    {
        PackageDefinitionId = packageDefinitionId,
        OrganizationId = OrganizationId,
        BranchId = BranchId,
        Name = name,
        CurrencyCode = CurrencyCode,
        PriceMinorUnits = price,
        IncludedSeconds = includedSeconds,
        BonusSeconds = bonusSeconds,
        ExpiresAfterDays = expiresAfterDays,
        IsActive = true,
        CreatedAtUtc = now.AddDays(-5)
    };

    private ShiftEntity SeedOpenShift(Guid staffUserId)
    {
        var shift = new ShiftEntity
        {
            ShiftId = StableGuid(9001),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            OpenedByStaffUserId = staffUserId,
            ClosedByStaffUserId = null,
            State = ShiftStateNames.Open,
            CurrencyCode = CurrencyCode,
            StartingCashMinorUnits = 100000,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 184000,
            DifferenceMinorUnits = 0,
            OpeningNote = "Открытие демо-смены",
            ClosingNote = string.Empty,
            OpenedAtUtc = now.AddHours(-6),
            ClosedAtUtc = null
        };

        dbContext.Shifts.Add(shift);
        dbContext.CashMovements.Add(new CashMovementEntity
        {
            CashMovementId = StableGuid(9101),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ShiftId = shift.ShiftId,
            CreatedByStaffUserId = staffUserId,
            MovementType = "cash_in",
            CurrencyCode = CurrencyCode,
            AmountMinorUnits = 50000,
            Reason = "Размен кассы",
            CreatedAtUtc = now.AddHours(-5)
        });

        return shift;
    }

    private void SeedSessions(Guid staffUserId, IReadOnlyCollection<SeatSeed> seats, IReadOnlyDictionary<Guid, DeviceEntity> devices, PlayerSeed players, Guid tariffVersionId)
    {
        AddSession(StableGuid(11001), "PC-001", SessionStateNames.Active, players.PrimaryPlayerId, 43, false);
        AddSession(StableGuid(11002), "PC-007", SessionStateNames.Active, players.VipPlayerId, 76, false);
        AddSession(StableGuid(11003), "PS-01", SessionStateNames.Ending, players.DebtPlayerId, 58, true);
        AddSession(StableGuid(11004), "VIP-01", SessionStateNames.Active, null, 118, false);
        AddSession(StableGuid(11005), "BOOT-01", SessionStateNames.Active, players.PrimaryPlayerId, 24, false);
        AddSession(StableGuid(11006), "PC-004", SessionStateNames.Active, players.GuestPlayerId, 15, false);

        void AddSession(Guid sessionId, string seatName, string state, Guid? playerAccountId, int remainingMinutes, bool pendingStop)
        {
            var seat = seats.Single(item => item.Name == seatName);
            if (!devices.TryGetValue(seat.SeatId, out var device))
            {
                return;
            }

            device.IsLocked = false;

            dbContext.Sessions.Add(new SessionEntity
            {
                SessionId = sessionId,
                OrganizationId = OrganizationId,
                BranchId = BranchId,
                SeatId = seat.SeatId,
                DeviceId = device.DeviceId,
                CreatedByStaffUserId = staffUserId,
                PlayerKind = playerAccountId is null ? "guest" : "registered",
                PlayerAccountId = playerAccountId,
                TariffRuleVersionId = tariffVersionId.ToString(),
                State = state,
                RequestedAtUtc = now.AddMinutes(-80),
                StartedAtUtc = now.AddMinutes(-80),
                EndsAtUtc = now.AddMinutes(remainingMinutes),
                EndedAtUtc = null,
                CurrentLeaseId = null,
                UpdatedAtUtc = now.AddMinutes(-2)
            });

            dbContext.SessionEvents.Add(new SessionEventEntity
            {
                SessionEventId = StableGuid(12000 + Math.Abs(sessionId.GetHashCode()) % 7000),
                SessionId = sessionId,
                OrganizationId = OrganizationId,
                BranchId = BranchId,
                EventType = state == SessionStateNames.Ending ? "session.end.requested" : "session.started",
                ActorStaffUserId = staffUserId,
                DeviceId = device.DeviceId,
                DetailsJson = "{}",
                CreatedAtUtc = now.AddMinutes(-79)
            });

            if (pendingStop)
            {
                dbContext.DeviceCommands.Add(new DeviceCommandEntity
                {
                    CommandId = StableGuid(13001),
                    DeviceId = device.DeviceId,
                    Type = DeviceCommandTypeNames.Lock,
                    PayloadJson = JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["sessionId"] = sessionId.ToString("D"),
                        ["reason"] = "session-end"
                    }),
                    Status = "Pending",
                    Message = null,
                    CreatedAtUtc = now.AddMinutes(-3),
                    UpdatedAtUtc = now.AddMinutes(-3)
                });
            }
        }
    }

    private void SeedLedger(Guid staffUserId, Guid shiftId, PlayerSeed players)
    {
        dbContext.LedgerEntries.AddRange(
            Ledger(StableGuid(14001), shiftId, players.PrimaryPlayerId, LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 30000, 0, "Пополнение депозита", "Стартовый депозит"),
            Ledger(StableGuid(14002), shiftId, players.PrimaryPlayerId, LedgerEntryTypeNames.GameplayCharge, LedgerAccountTypeNames.Wallet, -8200, 4920, "Игровое время", "PC-001"),
            Ledger(StableGuid(14003), shiftId, players.VipPlayerId, LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 100000, 0, "Пополнение депозита", "VIP клиент"),
            Ledger(StableGuid(14004), shiftId, players.DebtPlayerId, LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 18500, 3900, "Постоплата", "PS-01"),
            Ledger(StableGuid(14005), shiftId, players.DebtPlayerId, LedgerEntryTypeNames.DebtPayment, LedgerAccountTypeNames.Debt, -6500, 0, "Оплата долга", "наличные"));
    }

    private LedgerEntryEntity Ledger(Guid id, Guid shiftId, Guid playerId, string entryType, string accountType, long amount, int seconds, string description, string reason) => new()
    {
        LedgerEntryId = id,
        OrganizationId = OrganizationId,
        BranchId = BranchId,
        ShiftId = shiftId,
        PlayerAccountId = playerId,
        SessionId = null,
        PlayerPackageId = null,
        EntryType = entryType,
        AccountType = accountType,
        AmountMinorUnits = amount,
        QuantitySeconds = seconds,
        CurrencyCode = CurrencyCode,
        Description = description,
        Reason = reason,
        ReversesLedgerEntryId = null,
        CreatedByStaffUserId = OwnerStaffUserId,
        CreatedAtUtc = now.AddHours(-2)
    };

    private void SeedPos(Guid staffUserId, Guid shiftId, Guid playerAccountId)
    {
        var drinksCategory = StableGuid(15001);
        var snacksCategory = StableGuid(15002);
        var cola = StableGuid(15101);
        var water = StableGuid(15102);
        var energy = StableGuid(15103);
        var saleId = StableGuid(15201);

        dbContext.PosProductCategories.AddRange(
            Category(drinksCategory, "Напитки"),
            Category(snacksCategory, "Снеки"));

        dbContext.PosProducts.AddRange(
            Product(cola, drinksCategory, "Cola 0.5", "DRINK-COLA-05", 1200, true),
            Product(water, drinksCategory, "Вода 0.5", "DRINK-WATER-05", 600, true),
            Product(energy, snacksCategory, "Энергетический батончик", "SNACK-BAR", 1500, true));

        dbContext.StockMovements.AddRange(
            Stock(StableGuid(15301), cola, 48, 650, "Приход товара"),
            Stock(StableGuid(15302), water, 72, 250, "Приход товара"),
            Stock(StableGuid(15303), energy, 24, 900, "Приход товара"),
            Stock(StableGuid(15304), cola, -2, 650, "Продажа POS"));

        dbContext.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = saleId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ShiftId = shiftId,
            CreatedByStaffUserId = staffUserId,
            PlayerAccountId = playerAccountId,
            State = PosSaleStateNames.Paid,
            CurrencyCode = CurrencyCode,
            TotalMinorUnits = 3000,
            RefundReason = string.Empty,
            VoidReason = string.Empty,
            CreatedAtUtc = now.AddHours(-1),
            PaidAtUtc = now.AddMinutes(-55),
            RefundedAtUtc = null,
            VoidedAtUtc = null
        });

        dbContext.PosSaleLines.AddRange(
            SaleLine(StableGuid(15401), saleId, cola, "Cola 0.5", 2, 1200),
            SaleLine(StableGuid(15402), saleId, energy, "Энергетический батончик", 1, 600));

        dbContext.Payments.Add(new PaymentEntity
        {
            PaymentId = StableGuid(15501),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            PosSaleId = saleId,
            ShiftId = shiftId,
            CreatedByStaffUserId = staffUserId,
            PaymentKind = "payment",
            Provider = "manual",
            PaymentMethod = "cash",
            CurrencyCode = CurrencyCode,
            AmountMinorUnits = 3000,
            Note = "Оплата наличными",
            CreatedAtUtc = now.AddMinutes(-55)
        });

        dbContext.Receipts.Add(new ReceiptEntity
        {
            ReceiptId = StableGuid(15601),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            PosSaleId = saleId,
            ReceiptNumber = "POS-20260522-0001",
            ReceiptType = "sale",
            CurrencyCode = CurrencyCode,
            TotalMinorUnits = 3000,
            CreatedAtUtc = now.AddMinutes(-55)
        });
    }

    private PosProductCategoryEntity Category(Guid categoryId, string name) => new()
    {
        CategoryId = categoryId,
        OrganizationId = OrganizationId,
        BranchId = BranchId,
        Name = name,
        IsActive = true,
        CreatedAtUtc = now.AddDays(-4)
    };

    private PosProductEntity Product(Guid productId, Guid categoryId, string name, string sku, long price, bool trackStock) => new()
    {
        ProductId = productId,
        OrganizationId = OrganizationId,
        BranchId = BranchId,
        CategoryId = categoryId,
        Name = name,
        Sku = sku,
        CurrencyCode = CurrencyCode,
        PriceMinorUnits = price,
        TrackStock = trackStock,
        AllowNegativeStock = false,
        IsActive = true,
        CreatedAtUtc = now.AddDays(-4)
    };

    private StockMovementEntity Stock(Guid id, Guid productId, int quantity, long cost, string reason) => new()
    {
        StockMovementId = id,
        OrganizationId = OrganizationId,
        BranchId = BranchId,
        ProductId = productId,
        MovementType = quantity >= 0 ? "receipt" : "sale",
        QuantityDelta = quantity,
        CurrencyCode = CurrencyCode,
        UnitCostMinorUnits = cost,
        Reason = reason,
        CreatedByStaffUserId = OwnerStaffUserId,
        CreatedAtUtc = now.AddDays(-3)
    };

    private static PosSaleLineEntity SaleLine(Guid id, Guid saleId, Guid productId, string name, int quantity, long unitPrice) => new()
    {
        PosSaleLineId = id,
        PosSaleId = saleId,
        ProductId = productId,
        ProductName = name,
        Quantity = quantity,
        CurrencyCode = CurrencyCode,
        UnitPriceMinorUnits = unitPrice,
        LineTotalMinorUnits = quantity * unitPrice,
        TracksStock = true,
        AllowNegativeStock = false
    };

    private void SeedReservations(Guid staffUserId, IReadOnlyCollection<SeatSeed> seats, PlayerSeed players)
    {
        dbContext.Reservations.AddRange(
            Reservation(StableGuid(16001), seats.Single(seat => seat.Name == "PC-008").SeatId, players.VipPlayerId, "Мадина С.", ReservationStateNames.Confirmed, now.AddMinutes(35), 90, "Подтверждена оператором"),
            Reservation(StableGuid(16002), seats.Single(seat => seat.Name == "VIP-02").SeatId, null, "Гость VIP", ReservationStateNames.Pending, now.AddMinutes(70), 120, "Ждёт звонка"),
            Reservation(StableGuid(16003), null, null, "Команда 5x5", ReservationStateNames.Pending, now.AddHours(3), 180, "Нужны 5 ПК рядом"));

        ReservationEntity Reservation(Guid id, Guid? seatId, Guid? playerAccountId, string customerName, string state, DateTimeOffset startsAt, int durationMinutes, string note) => new()
        {
            ReservationId = id,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            PlayerAccountId = playerAccountId,
            SeatId = seatId,
            CustomerName = customerName,
            PhoneNumber = "+992900000404",
            StartsAtUtc = startsAt,
            EndsAtUtc = startsAt.AddMinutes(durationMinutes),
            State = state,
            Source = "operator",
            Note = note,
            CreatedByStaffUserId = staffUserId,
            UpdatedByStaffUserId = null,
            CreatedAtUtc = now.AddMinutes(-40),
            UpdatedAtUtc = now.AddMinutes(-40),
            CancelledAtUtc = null,
            CancelReason = string.Empty,
            SeatedAtUtc = null
        };
    }

    private void SeedUpdates(Guid platformAdminUserId, IReadOnlyDictionary<Guid, DeviceEntity> devices)
    {
        var packageId = StableGuid(17001);
        var rolloutId = StableGuid(17002);

        dbContext.UpdatePackages.Add(new UpdatePackageEntity
        {
            UpdatePackageId = packageId,
            Component = "organization-admin",
            Version = "0.2.0",
            Channel = "internal",
            ArtifactUri = "https://updates.afk4.test/organization-admin/0.2.0/organization-admin.msi",
            Sha256 = new string('a', 64),
            Signature = "dev-signature",
            SignatureAlgorithm = "ECDSA-P256-SHA256-IEEE-P1363",
            SizeBytes = 4096,
            State = UpdatePackageStateNames.Validated,
            ReleaseNotes = "Пакет оператора 0.2.0.",
            CreatedByPlatformAdminUserId = platformAdminUserId,
            CreatedAtUtc = now.AddHours(-4)
        });

        dbContext.UpdateRollouts.Add(new UpdateRolloutEntity
        {
            UpdateRolloutId = rolloutId,
            UpdatePackageId = packageId,
            Component = "organization-admin",
            Version = "0.2.0",
            Channel = "internal",
            State = UpdateRolloutStateNames.Active,
            TargetKind = "branch",
            BatchPercent = 25,
            Reason = "Пилотная раскатка",
            CreatedByPlatformAdminUserId = platformAdminUserId,
            CreatedAtUtc = now.AddHours(-3),
            StartsAtUtc = now.AddHours(-2),
            CompletedAtUtc = null
        });

        dbContext.UpdateRolloutTargets.Add(new UpdateRolloutTargetEntity
        {
            UpdateRolloutTargetId = StableGuid(17003),
            UpdateRolloutId = rolloutId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            TargetKind = "branch",
            DeviceId = null,
            CreatedAtUtc = now.AddHours(-3)
        });

        foreach (var device in devices.Values.Take(4))
        {
            dbContext.DeviceUpdateStatuses.Add(new DeviceUpdateStatusEntity
            {
                DeviceUpdateStatusId = StableGuid(17100 + Math.Abs(device.DeviceId.GetHashCode()) % 500),
                OrganizationId = OrganizationId,
                BranchId = BranchId,
                DeviceId = device.DeviceId,
                UpdateRolloutId = rolloutId,
                UpdatePackageId = packageId,
                Component = "organization-admin",
                InstalledVersion = "0.1.14",
                TargetVersion = "0.2.0",
                Status = device.IsOnline ? "installed" : "failed",
                Message = device.IsOnline ? "target reached" : "ПК не вышел на связь",
                FirstReportedAtUtc = now.AddHours(-2),
                UpdatedAtUtc = now.AddMinutes(-20)
            });
        }
    }

    private void SeedAudit(Guid staffUserId)
    {
        dbContext.AuditRecords.AddRange(
            Audit(StableGuid(18001), staffUserId, "sessions.start", "Session", StableGuid(11001).ToString(), "succeeded", """{"seat":"PC-001"}"""),
            Audit(StableGuid(18002), staffUserId, "pos.sale.create", "PosSale", StableGuid(15201).ToString(), "succeeded", """{"total":"30.00"}"""),
            Audit(StableGuid(18003), staffUserId, "devices.command.dispatch", "DeviceCommand", StableGuid(13001).ToString(), "succeeded", """{"type":"lock"}"""));
    }

    private AuditRecordEntity Audit(Guid auditId, Guid staffUserId, string action, string targetType, string targetId, string outcome, string detailsJson) => new()
    {
        AuditRecordId = auditId,
        OrganizationId = OrganizationId,
        BranchId = BranchId,
        ActorStaffUserId = staffUserId,
        Action = action,
        TargetType = targetType,
        TargetId = targetId,
        Outcome = outcome,
        SourceApp = "organization-admin",
        DetailsJson = detailsJson,
        CreatedAtUtc = now.AddMinutes(-30)
    };

    private static Guid StableGuid(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:x12}");

    private sealed record StaffSeed(StaffUserEntity OrganizationOwner, StaffUserEntity Operator, StaffUserEntity Technician);

    private sealed record PlayerSeed(Guid PrimaryPlayerId, Guid VipPlayerId, Guid DebtPlayerId, Guid GuestPlayerId);

    private sealed record BillingSeed(Guid StandardTariffVersionId);

    private sealed record ZoneSeed(Guid ZoneId, string Name, int SortOrder, int SeatIdBase, IReadOnlyList<string> SeatNames)
    {
        public List<SeatSeed> Seats { get; } = SeatNames
            .Select((name, index) => new SeatSeed(StableGuid(SeatIdBase + index), name, SeatIdBase + index))
            .ToList();
    }

    private sealed record SeatSeed(Guid SeatId, string Name, int Index)
    {
        public int SortOrder { get; set; }
    }
}
