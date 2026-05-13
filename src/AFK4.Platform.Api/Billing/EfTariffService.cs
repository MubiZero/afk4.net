using System.Data;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Tariffs;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Billing;

public sealed class EfTariffService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : ITariffService
{
    private const string TariffCreateOperation = "tariff-create";
    private const string TariffVersionCreateOperation = "tariff-version-create";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BillingCommandServiceResult<TariffDto>> CreateTariffAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateTariffRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await GetExistingIdempotencyAsync<TariffDto, CreateTariffRequest>(
            request.OrganizationId,
            branchId,
            TariffCreateOperation,
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (request.OrganizationId == Guid.Empty)
        {
            return BillingCommandServiceResult<TariffDto>.Invalid("Organization id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BillingCommandServiceResult<TariffDto>.Invalid("Tariff name is required.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            var tariff = new TariffEntity
            {
                TariffId = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                BranchId = branchId,
                Name = request.Name.Trim(),
                IsActive = true,
                CreatedAtUtc = now
            };

            dbContext.Tariffs.Add(tariff);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToDto(tariff);
            AddIdempotencyRecord(
                request.OrganizationId,
                branchId,
                TariffCreateOperation,
                request.IdempotencyKey,
                request,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<TariffDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<TariffDto, CreateTariffRequest>(
            request.OrganizationId,
            branchId,
            TariffCreateOperation,
            request.IdempotencyKey,
            request,
            cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<TariffVersionDto>> CreateTariffVersionAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateTariffVersionRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await GetExistingIdempotencyAsync<TariffVersionDto, CreateTariffVersionRequest>(
            request.OrganizationId,
            branchId,
            TariffVersionCreateOperation,
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (request.PricePerMinuteMinorUnits <= 0)
        {
            return BillingCommandServiceResult<TariffVersionDto>.Invalid("Price per minute must be positive.");
        }

        if (request.MinimumBillableMinutes <= 0)
        {
            return BillingCommandServiceResult<TariffVersionDto>.Invalid("Minimum billable minutes must be positive.");
        }

        if (request.RoundingIncrementMinutes <= 0)
        {
            return BillingCommandServiceResult<TariffVersionDto>.Invalid("Rounding increment minutes must be positive.");
        }

        if (string.IsNullOrWhiteSpace(request.CurrencyCode))
        {
            return BillingCommandServiceResult<TariffVersionDto>.Invalid("Currency code is required.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var tariffExists = await dbContext.Tariffs
                .AsNoTracking()
                .AnyAsync(
                    tariff =>
                        tariff.OrganizationId == request.OrganizationId &&
                        tariff.BranchId == branchId &&
                        tariff.TariffId == request.TariffId,
                    cancellationToken);

            if (!tariffExists)
            {
                return BillingCommandServiceResult<TariffVersionDto>.Missing("Tariff was not found.");
            }

            var nextVersionNumber = await dbContext.TariffVersions
                .Where(version => version.TariffId == request.TariffId)
                .Select(version => (int?)version.VersionNumber)
                .MaxAsync(cancellationToken) ?? 0;
            nextVersionNumber++;

            var activeVersions = await dbContext.TariffVersions
                .Where(version =>
                    version.OrganizationId == request.OrganizationId &&
                    version.BranchId == branchId &&
                    version.TariffId == request.TariffId &&
                    version.RetiredAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var activeVersion in activeVersions)
            {
                activeVersion.RetiredAtUtc = request.EffectiveFromUtc;
            }

            var now = timeProvider.GetUtcNow();
            var version = new TariffVersionEntity
            {
                TariffVersionId = Guid.NewGuid(),
                TariffId = request.TariffId,
                OrganizationId = request.OrganizationId,
                BranchId = branchId,
                VersionNumber = nextVersionNumber,
                CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
                PricePerMinuteMinorUnits = request.PricePerMinuteMinorUnits,
                MinimumBillableMinutes = request.MinimumBillableMinutes,
                RoundingIncrementMinutes = request.RoundingIncrementMinutes,
                EffectiveFromUtc = request.EffectiveFromUtc,
                RetiredAtUtc = null,
                CreatedAtUtc = now
            };

            dbContext.TariffVersions.Add(version);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToDto(version);
            AddIdempotencyRecord(
                request.OrganizationId,
                branchId,
                TariffVersionCreateOperation,
                request.IdempotencyKey,
                request,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<TariffVersionDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<TariffVersionDto, CreateTariffVersionRequest>(
            request.OrganizationId,
            branchId,
            TariffVersionCreateOperation,
            request.IdempotencyKey,
            request,
            cancellationToken),
            IsolationLevel.Serializable,
            cancellationToken);
    }

    public async Task<TariffCalculationResult?> CalculateAsync(
        Guid branchId,
        CalculateTariffRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DurationMinutes <= 0)
        {
            return null;
        }

        var version = await dbContext.TariffVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.TariffVersionId == request.TariffVersionId,
                cancellationToken);

        if (version is null || version.RetiredAtUtc is not null)
        {
            return null;
        }

        var billableMinutes = Math.Max(request.DurationMinutes, version.MinimumBillableMinutes);
        if (version.RoundingIncrementMinutes > 1)
        {
            billableMinutes = RoundUp(billableMinutes, version.RoundingIncrementMinutes);
        }

        return new TariffCalculationResult(
            version.TariffId,
            version.TariffVersionId,
            version.TariffVersionId.ToString("D"),
            request.DurationMinutes,
            billableMinutes,
            new MoneyDto(version.CurrencyCode, billableMinutes * version.PricePerMinuteMinorUnits));
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
        var existing = await dbContext.BillingCommandIdempotency
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == branchId &&
                    candidate.Operation == operation &&
                    candidate.IdempotencyKeyHash == idempotencyKeyHash,
                cancellationToken);

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

    private static TariffDto ToDto(TariffEntity tariff)
    {
        return new TariffDto(
            tariff.TariffId,
            tariff.OrganizationId,
            tariff.BranchId,
            tariff.Name,
            tariff.IsActive,
            tariff.CreatedAtUtc);
    }

    private static TariffVersionDto ToDto(TariffVersionEntity version)
    {
        return new TariffVersionDto(
            version.TariffVersionId,
            version.TariffId,
            version.VersionNumber,
            version.CurrencyCode,
            version.PricePerMinuteMinorUnits,
            version.MinimumBillableMinutes,
            version.RoundingIncrementMinutes,
            version.EffectiveFromUtc,
            version.RetiredAtUtc,
            version.CreatedAtUtc);
    }

    private static int RoundUp(int value, int increment)
    {
        return ((value + increment - 1) / increment) * increment;
    }

    private static string HashRequest<TRequest>(TRequest request)
    {
        return BillingCommandIdempotencyKeyHasher.Hash(JsonSerializer.Serialize(request, JsonOptions));
    }
}
