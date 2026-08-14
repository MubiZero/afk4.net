using System.Data;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Packages;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Billing;

public sealed class EfPackageService(
    PlatformDbContext dbContext,
    IOpenShiftResolver openShiftResolver,
    TimeProvider timeProvider) : IPackageService
{
    private const string PackageCreateOperation = "package-create";
    private const string PackagePurchaseOperation = "package-purchase";
    private const string PackageConsumeOperation = "package-consume";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BillingCommandServiceResult<PackageDefinitionDto>> CreatePackageDefinitionAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreatePackageDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await GetExistingIdempotencyAsync<PackageDefinitionDto, CreatePackageDefinitionRequest>(
            request.OrganizationId,
            branchId,
            PackageCreateOperation,
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (request.OrganizationId == Guid.Empty)
        {
            return BillingCommandServiceResult<PackageDefinitionDto>.Invalid("Organization id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BillingCommandServiceResult<PackageDefinitionDto>.Invalid("Package name is required.");
        }

        if (request.Price.MinorUnits <= 0)
        {
            return BillingCommandServiceResult<PackageDefinitionDto>.Invalid("Package price must be positive.");
        }

        if (request.IncludedSeconds <= 0)
        {
            return BillingCommandServiceResult<PackageDefinitionDto>.Invalid("Included seconds must be positive.");
        }

        if (request.BonusSeconds < 0)
        {
            return BillingCommandServiceResult<PackageDefinitionDto>.Invalid("Bonus seconds cannot be negative.");
        }

        if (request.ExpiresAfterDays <= 0)
        {
            return BillingCommandServiceResult<PackageDefinitionDto>.Invalid("Package expiry days must be positive.");
        }

        if (!IsValidCurrencyCode(request.Price.CurrencyCode))
        {
            return BillingCommandServiceResult<PackageDefinitionDto>.Invalid("Currency code must be exactly 3 alphabetic letters.");
        }

        var normalizedName = NormalizePackageName(request.Name);
        var existingNames = await dbContext.PackageDefinitions
            .AsNoTracking()
            .Where(package =>
                package.OrganizationId == request.OrganizationId &&
                package.BranchId == branchId)
            .Select(package => package.Name)
            .ToListAsync(cancellationToken);

        if (existingNames.Any(existingName =>
            string.Equals(NormalizePackageName(existingName), normalizedName, StringComparison.Ordinal)))
        {
            return BillingCommandServiceResult<PackageDefinitionDto>.Invalid("Package name already exists.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            var package = new PackageDefinitionEntity
            {
                PackageDefinitionId = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                BranchId = branchId,
                Name = normalizedName,
                CurrencyCode = request.Price.CurrencyCode.Trim().ToUpperInvariant(),
                PriceMinorUnits = request.Price.MinorUnits,
                IncludedSeconds = request.IncludedSeconds,
                BonusSeconds = request.BonusSeconds,
                ExpiresAfterDays = request.ExpiresAfterDays,
                IsActive = true,
                CreatedAtUtc = now
            };

            dbContext.PackageDefinitions.Add(package);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToDto(package);
            AddIdempotencyRecord(
                request.OrganizationId,
                branchId,
                PackageCreateOperation,
                request.IdempotencyKey,
                request,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<PackageDefinitionDto>.Ok(response);
        }, () => RecoverPackageCreateRaceAsync(branchId, request, cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<PackageDefinitionDto>> UpdatePackageDefinitionAsync(
        Guid branchId,
        Guid packageDefinitionId,
        Guid actorStaffUserId,
        UpdatePackageDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        if (packageDefinitionId == Guid.Empty)
        {
            return BillingCommandServiceResult<PackageDefinitionDto>.Invalid("Package definition id is required.");
        }

        var validation = ValidateUpdatePackageDefinitionRequest(request);
        if (validation is not null)
        {
            return BillingCommandServiceResult<PackageDefinitionDto>.Invalid(validation);
        }

        var package = await dbContext.PackageDefinitions
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.PackageDefinitionId == packageDefinitionId,
                cancellationToken);

        if (package is null)
        {
            return BillingCommandServiceResult<PackageDefinitionDto>.Missing("Package definition was not found.");
        }

        var normalizedName = NormalizePackageName(request.Name);
        var nameExists = await dbContext.PackageDefinitions
            .AsNoTracking()
            .AnyAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.PackageDefinitionId != packageDefinitionId &&
                    candidate.Name == normalizedName,
                cancellationToken);

        if (nameExists)
        {
            return BillingCommandServiceResult<PackageDefinitionDto>.Invalid("Package name already exists.");
        }

        package.Name = normalizedName;
        package.CurrencyCode = request.Price.CurrencyCode.Trim().ToUpperInvariant();
        package.PriceMinorUnits = request.Price.MinorUnits;
        package.IncludedSeconds = request.IncludedSeconds;
        package.BonusSeconds = request.BonusSeconds;
        package.ExpiresAfterDays = request.ExpiresAfterDays;
        package.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return BillingCommandServiceResult<PackageDefinitionDto>.Ok(ToDto(package));
    }

    public async Task<BillingCommandServiceResult<PlayerPackageDto>> PurchasePackageAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        PurchasePackageRequest request,
        CancellationToken cancellationToken)
    {
        var requestHashInput = PlayerScopedRequest(playerAccountId, request);
        var idempotency = await GetExistingIdempotencyAsync<PlayerPackageDto, object>(
            request.OrganizationId,
            branchId,
            PackagePurchaseOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        var player = await FindPlayerAsync(request.OrganizationId, branchId, playerAccountId, cancellationToken);
        if (player is null)
        {
            return BillingCommandServiceResult<PlayerPackageDto>.Missing("Player account was not found.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var package = await dbContext.PackageDefinitions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == request.OrganizationId &&
                        candidate.BranchId == branchId &&
                        candidate.PackageDefinitionId == request.PackageDefinitionId &&
                        candidate.IsActive,
                    cancellationToken);

            if (package is null)
            {
                return BillingCommandServiceResult<PlayerPackageDto>.Missing("Package definition was not found.");
            }

            var currencyValidation = await GetLedgerCurrencyForWriteAsync<PlayerPackageDto>(
                playerAccountId,
                package.CurrencyCode,
                cancellationToken);
            if (currencyValidation.Error is not null)
            {
                return currencyValidation.Error;
            }

            var walletBalance = await GetWalletBalanceAsync(playerAccountId, cancellationToken);
            if (walletBalance < package.PriceMinorUnits)
            {
                return BillingCommandServiceResult<PlayerPackageDto>.Invalid("Insufficient wallet balance.");
            }

            var openShift = await RequireOpenShiftAsync<PlayerPackageDto>(
                request.OrganizationId,
                branchId,
                cancellationToken);
            if (openShift.Error is not null)
            {
                return openShift.Error;
            }

            var now = timeProvider.GetUtcNow();
            var playerPackage = new PlayerPackageEntity
            {
                PlayerPackageId = Guid.NewGuid(),
                PackageDefinitionId = package.PackageDefinitionId,
                OrganizationId = package.OrganizationId,
                BranchId = package.BranchId,
                PlayerAccountId = playerAccountId,
                Name = package.Name,
                CurrencyCode = package.CurrencyCode,
                PurchasedPriceMinorUnits = package.PriceMinorUnits,
                IncludedSeconds = package.IncludedSeconds,
                BonusSeconds = package.BonusSeconds,
                PurchasedAtUtc = now,
                ExpiresAtUtc = now.AddDays(package.ExpiresAfterDays)
            };

            dbContext.PlayerPackages.Add(playerPackage);

            var entries = new List<LedgerEntryEntity>
            {
                BillingEntryFactory.Create(
                    package.OrganizationId,
                    package.BranchId,
                    playerAccountId,
                    sessionId: null,
                    playerPackage.PlayerPackageId,
                    LedgerEntryTypeNames.PackagePurchase,
                    LedgerAccountTypeNames.Wallet,
                    -package.PriceMinorUnits,
                    quantitySeconds: 0,
                    package.CurrencyCode,
                    LedgerEntryTypeNames.PackagePurchase,
                    "package purchase",
                    reversesLedgerEntryId: null,
                    actorStaffUserId,
                    now,
                    openShift.ShiftId),
                BillingEntryFactory.Create(
                    package.OrganizationId,
                    package.BranchId,
                    playerAccountId,
                    sessionId: null,
                    playerPackage.PlayerPackageId,
                    LedgerEntryTypeNames.PackagePurchase,
                    LedgerAccountTypeNames.PackageTime,
                    amountMinorUnits: 0,
                    package.IncludedSeconds,
                    package.CurrencyCode,
                    LedgerEntryTypeNames.PackagePurchase,
                    "package included time grant",
                    reversesLedgerEntryId: null,
                    actorStaffUserId,
                    now.AddTicks(1),
                    openShift.ShiftId)
            };

            if (package.BonusSeconds > 0)
            {
                entries.Add(BillingEntryFactory.Create(
                    package.OrganizationId,
                    package.BranchId,
                    playerAccountId,
                    sessionId: null,
                    playerPackage.PlayerPackageId,
                    LedgerEntryTypeNames.BonusGrant,
                    LedgerAccountTypeNames.BonusTime,
                    amountMinorUnits: 0,
                    package.BonusSeconds,
                    package.CurrencyCode,
                    LedgerEntryTypeNames.BonusGrant,
                    "package bonus time grant",
                    reversesLedgerEntryId: null,
                    actorStaffUserId,
                    now.AddTicks(2),
                    openShift.ShiftId));
            }

            dbContext.LedgerEntries.AddRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = await ToPlayerPackageDtoAsync(playerPackage, cancellationToken);
            AddIdempotencyRecord(
                request.OrganizationId,
                branchId,
                PackagePurchaseOperation,
                request.IdempotencyKey,
                requestHashInput,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<PlayerPackageDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<PlayerPackageDto, object>(
            request.OrganizationId,
            branchId,
            PackagePurchaseOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken),
            IsolationLevel.Serializable,
            cancellationToken);
    }

    public async Task<BillingCommandServiceResult<IReadOnlyList<LedgerEntryDto>>> ConsumePackageTimeAsync(
        Guid playerAccountId,
        Guid playerPackageId,
        Guid branchId,
        Guid sessionId,
        Guid actorStaffUserId,
        int durationSeconds,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var requestHashInput = new
        {
            PlayerAccountId = playerAccountId,
            PlayerPackageId = playerPackageId,
            SessionId = sessionId,
            DurationSeconds = durationSeconds
        };

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BillingCommandServiceResult<IReadOnlyList<LedgerEntryDto>>.Invalid("Idempotency key is required.");
        }

        if (durationSeconds <= 0)
        {
            return BillingCommandServiceResult<IReadOnlyList<LedgerEntryDto>>.Invalid("Duration seconds must be positive.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var playerPackage = await dbContext.PlayerPackages
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    package =>
                        package.PlayerAccountId == playerAccountId &&
                        package.PlayerPackageId == playerPackageId &&
                        package.BranchId == branchId,
                    cancellationToken);

            if (playerPackage is null)
            {
                return BillingCommandServiceResult<IReadOnlyList<LedgerEntryDto>>.Missing("Player package was not found.");
            }

            var organizationId = playerPackage.OrganizationId;
            var consumeIdempotency = await GetExistingIdempotencyAsync<IReadOnlyList<LedgerEntryDto>, object>(
                organizationId,
                branchId,
                PackageConsumeOperation,
                idempotencyKey,
                requestHashInput,
                cancellationToken);

            if (consumeIdempotency is not null)
            {
                return consumeIdempotency;
            }

            var now = timeProvider.GetUtcNow();
            if (playerPackage.ExpiresAtUtc is not null && playerPackage.ExpiresAtUtc <= now)
            {
                return BillingCommandServiceResult<IReadOnlyList<LedgerEntryDto>>.Invalid("Player package is expired.");
            }

            var remaining = await LedgerBalanceProjector.GetPackageRemainingSecondsAsync(dbContext, playerPackageId, cancellationToken);
            if (remaining.IncludedSeconds + remaining.BonusSeconds < durationSeconds)
            {
                return BillingCommandServiceResult<IReadOnlyList<LedgerEntryDto>>.Invalid("Insufficient package time remaining.");
            }

            var openShift = await RequireOpenShiftAsync<IReadOnlyList<LedgerEntryDto>>(
                playerPackage.OrganizationId,
                branchId,
                cancellationToken);
            if (openShift.Error is not null)
            {
                return openShift.Error;
            }

            var bonusToConsume = Math.Min(remaining.BonusSeconds, durationSeconds);
            var packageToConsume = durationSeconds - bonusToConsume;
            var entries = new List<LedgerEntryEntity>();

            if (bonusToConsume > 0)
            {
                entries.Add(BillingEntryFactory.Create(
                    playerPackage.OrganizationId,
                    playerPackage.BranchId,
                    playerAccountId,
                    sessionId,
                    playerPackageId,
                    LedgerEntryTypeNames.BonusConsumption,
                    LedgerAccountTypeNames.BonusTime,
                    amountMinorUnits: 0,
                    -bonusToConsume,
                    playerPackage.CurrencyCode,
                    LedgerEntryTypeNames.BonusConsumption,
                    "package bonus time consumption",
                    reversesLedgerEntryId: null,
                    actorStaffUserId,
                    now,
                    openShift.ShiftId));
            }

            if (packageToConsume > 0)
            {
                entries.Add(BillingEntryFactory.Create(
                    playerPackage.OrganizationId,
                    playerPackage.BranchId,
                    playerAccountId,
                    sessionId,
                    playerPackageId,
                    LedgerEntryTypeNames.PackageConsumption,
                    LedgerAccountTypeNames.PackageTime,
                    amountMinorUnits: 0,
                    -packageToConsume,
                    playerPackage.CurrencyCode,
                    LedgerEntryTypeNames.PackageConsumption,
                    "package included time consumption",
                    reversesLedgerEntryId: null,
                    actorStaffUserId,
                    now.AddTicks(1),
                    openShift.ShiftId));
            }

            dbContext.LedgerEntries.AddRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken);

            IReadOnlyList<LedgerEntryDto> response = entries.Select(LedgerBalanceProjector.ToDto).ToList();
            AddIdempotencyRecord(
                playerPackage.OrganizationId,
                branchId,
                PackageConsumeOperation,
                idempotencyKey,
                requestHashInput,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<IReadOnlyList<LedgerEntryDto>>.Ok(response);
        }, () => ReplayConsumeIdempotencyAsync(
            playerAccountId,
            playerPackageId,
            branchId,
            idempotencyKey,
            requestHashInput,
            cancellationToken),
            IsolationLevel.Serializable,
            cancellationToken);
    }

    private async Task<PlayerPackageDto> ToPlayerPackageDtoAsync(
        PlayerPackageEntity playerPackage,
        CancellationToken cancellationToken)
    {
        var remaining = await LedgerBalanceProjector.GetPackageRemainingSecondsAsync(
            dbContext,
            playerPackage.PlayerPackageId,
            cancellationToken);

        return new PlayerPackageDto(
            playerPackage.PlayerPackageId,
            playerPackage.PackageDefinitionId,
            playerPackage.PlayerAccountId,
            playerPackage.Name,
            new MoneyDto(playerPackage.CurrencyCode, playerPackage.PurchasedPriceMinorUnits),
            playerPackage.IncludedSeconds,
            playerPackage.BonusSeconds,
            remaining.IncludedSeconds,
            remaining.BonusSeconds,
            playerPackage.PurchasedAtUtc,
            playerPackage.ExpiresAtUtc);
    }

    private async Task<BillingCommandServiceResult<TResponse>?> GetExistingIdempotencyAsync<TResponse, TRequest>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BillingCommandServiceResult<TResponse>.Invalid("Idempotency key is required.");
        }

        var requestHash = HashRequest(request);
        var idempotencyKeyHash = BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey);
        var query = dbContext.BillingCommandIdempotency
            .AsNoTracking()
            .Where(candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.BranchId == branchId &&
                candidate.Operation == operation &&
                candidate.IdempotencyKeyHash == idempotencyKeyHash);

        var existing = await query.SingleOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return BillingCommandServiceResult<TResponse>.RequestConflict("Idempotency key was already used for a different request.");
        }

        var response = JsonSerializer.Deserialize<TResponse>(existing.ResponseJson, JsonOptions);

        return response is null
            ? BillingCommandServiceResult<TResponse>.Invalid("Stored idempotent response could not be read.")
            : BillingCommandServiceResult<TResponse>.Ok(response);
    }

    private async Task<PlayerAccountEntity?> FindPlayerAsync(
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        return await dbContext.PlayerAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                player =>
                    player.OrganizationId == organizationId &&
                    player.HomeBranchId == branchId &&
                    player.PlayerAccountId == playerAccountId,
                cancellationToken);
    }

    private async Task<long> GetWalletBalanceAsync(Guid playerAccountId, CancellationToken cancellationToken)
    {
        return await dbContext.LedgerEntries
            .Where(entry =>
                entry.PlayerAccountId == playerAccountId &&
                entry.AccountType == LedgerAccountTypeNames.Wallet)
            .SumAsync(entry => (long?)entry.AmountMinorUnits, cancellationToken) ?? 0;
    }

    private void AddIdempotencyRecord<TRequest, TResponse>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        TResponse response,
        DateTimeOffset now)
    {
        dbContext.BillingCommandIdempotency.Add(new BillingCommandIdempotencyEntity
        {
            BillingCommandIdempotencyId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            Operation = operation,
            IdempotencyKeyHash = BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey),
            RequestHash = HashRequest(request),
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1)
        });
    }

    private async Task<BillingCommandServiceResult<TResponse>?> ReplayIdempotencyAsync<TResponse, TRequest>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        CancellationToken cancellationToken)
    {
        return await GetExistingIdempotencyAsync<TResponse, TRequest>(
            organizationId,
            branchId,
            operation,
            idempotencyKey,
            request,
            cancellationToken);
    }

    private async Task<BillingCommandServiceResult<PackageDefinitionDto>?> RecoverPackageCreateRaceAsync(
        Guid branchId,
        CreatePackageDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await ReplayIdempotencyAsync<PackageDefinitionDto, CreatePackageDefinitionRequest>(
            request.OrganizationId,
            branchId,
            PackageCreateOperation,
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        var normalizedName = NormalizePackageName(request.Name);
        var existingNames = await dbContext.PackageDefinitions
            .AsNoTracking()
            .Where(package =>
                package.OrganizationId == request.OrganizationId &&
                package.BranchId == branchId)
            .Select(package => package.Name)
            .ToListAsync(cancellationToken);

        return existingNames.Any(existingName =>
            string.Equals(NormalizePackageName(existingName), normalizedName, StringComparison.Ordinal))
            ? BillingCommandServiceResult<PackageDefinitionDto>.Invalid("Package name already exists.")
            : null;
    }

    private async Task<BillingCommandServiceResult<IReadOnlyList<LedgerEntryDto>>?> ReplayConsumeIdempotencyAsync(
        Guid playerAccountId,
        Guid playerPackageId,
        Guid branchId,
        string idempotencyKey,
        object requestHashInput,
        CancellationToken cancellationToken)
    {
        var organizationId = await dbContext.PlayerPackages
            .AsNoTracking()
            .Where(package =>
                package.PlayerAccountId == playerAccountId &&
                package.PlayerPackageId == playerPackageId &&
                package.BranchId == branchId)
            .Select(package => (Guid?)package.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);

        if (organizationId is null)
        {
            return null;
        }

        return await GetExistingIdempotencyAsync<IReadOnlyList<LedgerEntryDto>, object>(
            organizationId.Value,
            branchId,
            PackageConsumeOperation,
            idempotencyKey,
            requestHashInput,
            cancellationToken);
    }

    private sealed record OpenShiftValidation<TResponse>(
        BillingCommandServiceResult<TResponse>? Error,
        Guid ShiftId);

    private async Task<OpenShiftValidation<TResponse>> RequireOpenShiftAsync<TResponse>(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var openShift = await openShiftResolver.GetOpenShiftIdAsync(organizationId, branchId, cancellationToken);

        return openShift.Succeeded && openShift.Response != Guid.Empty
            ? new OpenShiftValidation<TResponse>(null, openShift.Response)
            : new OpenShiftValidation<TResponse>(
                BillingCommandServiceResult<TResponse>.Invalid(openShift.Error ?? EfShiftService.OpenShiftRequiredCode),
                Guid.Empty);
    }

    private sealed record LedgerCurrencyValidation<TResponse>(
        BillingCommandServiceResult<TResponse>? Error,
        string CurrencyCode);

    private async Task<LedgerCurrencyValidation<TResponse>> GetLedgerCurrencyForWriteAsync<TResponse>(
        Guid playerAccountId,
        string requestedCurrencyCode,
        CancellationToken cancellationToken)
    {
        var normalizedRequestedCurrency = requestedCurrencyCode.Trim().ToUpperInvariant();
        var rawCurrencies = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerAccountId == playerAccountId)
            .Select(entry => entry.CurrencyCode)
            .ToListAsync(cancellationToken);
        var currencies = rawCurrencies
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (currencies.Count > 1)
        {
            return new LedgerCurrencyValidation<TResponse>(
                BillingCommandServiceResult<TResponse>.Invalid("Player ledger contains multiple currencies."),
                normalizedRequestedCurrency);
        }

        var existingCurrency = currencies.SingleOrDefault();
        if (existingCurrency is not null &&
            !string.Equals(existingCurrency, normalizedRequestedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return new LedgerCurrencyValidation<TResponse>(
                BillingCommandServiceResult<TResponse>.Invalid("Requested currency must match the player ledger currency."),
                existingCurrency.ToUpperInvariant());
        }

        return new LedgerCurrencyValidation<TResponse>(
            Error: null,
            CurrencyCode: existingCurrency?.ToUpperInvariant() ?? normalizedRequestedCurrency);
    }

    private static PackageDefinitionDto ToDto(PackageDefinitionEntity package)
    {
        return new PackageDefinitionDto(
            package.PackageDefinitionId,
            package.OrganizationId,
            package.BranchId,
            package.Name,
            new MoneyDto(package.CurrencyCode, package.PriceMinorUnits),
            package.IncludedSeconds,
            package.BonusSeconds,
            package.ExpiresAfterDays,
            package.IsActive,
            package.CreatedAtUtc);
    }

    private static string? ValidateUpdatePackageDefinitionRequest(UpdatePackageDefinitionRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "Organization id is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Package name is required.";
        }

        if (request.Price.MinorUnits <= 0)
        {
            return "Package price must be positive.";
        }

        if (request.IncludedSeconds <= 0)
        {
            return "Included seconds must be positive.";
        }

        if (request.BonusSeconds < 0)
        {
            return "Bonus seconds cannot be negative.";
        }

        if (request.ExpiresAfterDays <= 0)
        {
            return "Package expiry days must be positive.";
        }

        if (!IsValidCurrencyCode(request.Price.CurrencyCode))
        {
            return "Currency code must be exactly 3 alphabetic letters.";
        }

        return null;
    }

    private static bool IsValidCurrencyCode(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return false;
        }

        var trimmed = currencyCode.Trim();
        return trimmed.Length == 3 &&
            trimmed.All(character =>
                (character >= 'A' && character <= 'Z') ||
                (character >= 'a' && character <= 'z'));
    }

    private static string HashRequest<TRequest>(TRequest request)
    {
        return BillingCommandIdempotencyKeyHasher.Hash(JsonSerializer.Serialize(request, JsonOptions));
    }

    private static string NormalizePackageName(string name)
    {
        return name.Trim().ToUpperInvariant();
    }

    private static object PlayerScopedRequest<TRequest>(Guid playerAccountId, TRequest request)
    {
        return new
        {
            PlayerAccountId = playerAccountId,
            Request = request
        };
    }

    private async Task<BillingCommandServiceResult<TResponse>> ExecuteInTransactionAsync<TResponse>(
        Func<Task<BillingCommandServiceResult<TResponse>>> action,
        Func<Task<BillingCommandServiceResult<TResponse>?>>? recoverIdempotencyRaceAsync,
        IsolationLevel? isolationLevel,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            try
            {
                return await action();
            }
            catch (DbUpdateException) when (recoverIdempotencyRaceAsync is not null)
            {
                dbContext.ChangeTracker.Clear();
                var recovered = await recoverIdempotencyRaceAsync();

                if (recovered is not null)
                {
                    return recovered;
                }

                throw;
            }
        }

        await using var transaction = isolationLevel is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : await dbContext.Database.BeginTransactionAsync(isolationLevel.Value, cancellationToken);
        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch (DbUpdateException) when (recoverIdempotencyRaceAsync is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            var recovered = await recoverIdempotencyRaceAsync();

            if (recovered is not null)
            {
                return recovered;
            }

            throw;
        }
    }

    private Task<BillingCommandServiceResult<TResponse>> ExecuteInTransactionAsync<TResponse>(
        Func<Task<BillingCommandServiceResult<TResponse>>> action,
        Func<Task<BillingCommandServiceResult<TResponse>?>>? recoverIdempotencyRaceAsync,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        return ExecuteInTransactionAsync(action, recoverIdempotencyRaceAsync, (IsolationLevel?)isolationLevel, cancellationToken);
    }

    private Task<BillingCommandServiceResult<TResponse>> ExecuteInTransactionAsync<TResponse>(
        Func<Task<BillingCommandServiceResult<TResponse>>> action,
        Func<Task<BillingCommandServiceResult<TResponse>?>>? recoverIdempotencyRaceAsync,
        CancellationToken cancellationToken)
    {
        return ExecuteInTransactionAsync(action, recoverIdempotencyRaceAsync, isolationLevel: null, cancellationToken);
    }
}
