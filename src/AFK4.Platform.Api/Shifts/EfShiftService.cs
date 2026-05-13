using System.Data;
using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Shifts;

public sealed class EfShiftService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IShiftService, IOpenShiftResolver
{
    private const string ShiftOpenOperation = "shift-open";
    private const string CashMovementOperation = "shift-cash-movement";
    private const string ShiftCloseOperation = "shift-close";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BillingCommandServiceResult<ShiftDto>> OpenShiftAsync(
        Guid branchId,
        Guid actorStaffUserId,
        OpenShiftRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await GetExistingIdempotencyAsync<ShiftDto, OpenShiftRequest>(
            request.OrganizationId,
            branchId,
            ShiftOpenOperation,
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (request.OrganizationId == Guid.Empty)
        {
            return BillingCommandServiceResult<ShiftDto>.Invalid("Organization id is required.");
        }

        if (request.StartingCash.MinorUnits < 0)
        {
            return BillingCommandServiceResult<ShiftDto>.Invalid("Starting cash amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(request.StartingCash.CurrencyCode))
        {
            return BillingCommandServiceResult<ShiftDto>.Invalid("Currency code is required.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var hasOpenShift = await dbContext.Shifts
                .AnyAsync(
                    shift =>
                        shift.OrganizationId == request.OrganizationId &&
                        shift.BranchId == branchId &&
                        shift.State == ShiftStateNames.Open,
                    cancellationToken);

            if (hasOpenShift)
            {
                return BillingCommandServiceResult<ShiftDto>.Invalid("An open shift already exists for this branch.");
            }

            var now = timeProvider.GetUtcNow();
            var shift = new ShiftEntity
            {
                ShiftId = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                BranchId = branchId,
                OpenedByStaffUserId = actorStaffUserId,
                ClosedByStaffUserId = null,
                State = ShiftStateNames.Open,
                CurrencyCode = request.StartingCash.CurrencyCode.Trim().ToUpperInvariant(),
                StartingCashMinorUnits = request.StartingCash.MinorUnits,
                CountedCashMinorUnits = 0,
                ExpectedCashMinorUnits = 0,
                DifferenceMinorUnits = 0,
                OpeningNote = request.OpeningNote.Trim(),
                ClosingNote = string.Empty,
                OpenedAtUtc = now,
                ClosedAtUtc = null
            };

            dbContext.Shifts.Add(shift);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToDto(shift);
            AddIdempotencyRecord(
                request.OrganizationId,
                branchId,
                ShiftOpenOperation,
                request.IdempotencyKey,
                request,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<ShiftDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<ShiftDto, OpenShiftRequest>(
            request.OrganizationId,
            branchId,
            ShiftOpenOperation,
            request.IdempotencyKey,
            request,
            cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<ShiftDto?>> GetCurrentShiftAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var shift = await dbContext.Shifts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == branchId &&
                    candidate.State == ShiftStateNames.Open,
                cancellationToken);

        return BillingCommandServiceResult<ShiftDto?>.Ok(shift is null ? null : ToDto(shift));
    }

    public async Task<BillingCommandServiceResult<Guid>> GetOpenShiftIdAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var shiftId = await dbContext.Shifts
            .AsNoTracking()
            .Where(
                shift =>
                    shift.OrganizationId == organizationId &&
                    shift.BranchId == branchId &&
                    shift.State == ShiftStateNames.Open)
            .Select(shift => (Guid?)shift.ShiftId)
            .SingleOrDefaultAsync(cancellationToken);

        return shiftId is null
            ? BillingCommandServiceResult<Guid>.Invalid("An open shift is required.")
            : BillingCommandServiceResult<Guid>.Ok(shiftId.Value);
    }

    public async Task<BillingCommandServiceResult<CashMovementDto>> RecordCashMovementAsync(
        Guid shiftId,
        Guid actorStaffUserId,
        RecordCashMovementRequest request,
        CancellationToken cancellationToken)
    {
        var requestHashInput = ShiftScopedRequest(shiftId, request);
        var shiftBranch = await GetShiftBranchAsync(request.OrganizationId, shiftId, cancellationToken);
        var branchId = shiftBranch ?? Guid.Empty;
        var idempotency = await GetExistingIdempotencyAsync<CashMovementDto, object>(
            request.OrganizationId,
            branchId,
            CashMovementOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (shiftBranch is null)
        {
            return BillingCommandServiceResult<CashMovementDto>.Missing("Shift was not found.");
        }

        if (request.Amount.MinorUnits <= 0)
        {
            return BillingCommandServiceResult<CashMovementDto>.Invalid("Cash movement amount must be positive.");
        }

        if (request.MovementType is not CashMovementTypeNames.CashIn and not CashMovementTypeNames.CashOut)
        {
            return BillingCommandServiceResult<CashMovementDto>.Invalid("Unsupported cash movement type.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BillingCommandServiceResult<CashMovementDto>.Invalid("Reason is required.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var shift = await dbContext.Shifts
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == request.OrganizationId &&
                        candidate.ShiftId == shiftId,
                    cancellationToken);

            if (shift is null)
            {
                return BillingCommandServiceResult<CashMovementDto>.Missing("Shift was not found.");
            }

            if (shift.State != ShiftStateNames.Open)
            {
                return BillingCommandServiceResult<CashMovementDto>.Invalid("Cash movements require an open shift.");
            }

            if (!string.Equals(shift.CurrencyCode, request.Amount.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return BillingCommandServiceResult<CashMovementDto>.Invalid("Cash movement currency must match the shift currency.");
            }

            var now = timeProvider.GetUtcNow();
            var movement = new CashMovementEntity
            {
                CashMovementId = Guid.NewGuid(),
                OrganizationId = shift.OrganizationId,
                BranchId = shift.BranchId,
                ShiftId = shift.ShiftId,
                CreatedByStaffUserId = actorStaffUserId,
                MovementType = request.MovementType,
                CurrencyCode = shift.CurrencyCode,
                AmountMinorUnits = request.Amount.MinorUnits,
                Reason = request.Reason.Trim(),
                CreatedAtUtc = now
            };

            dbContext.CashMovements.Add(movement);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToDto(movement);
            AddIdempotencyRecord(
                request.OrganizationId,
                shift.BranchId,
                CashMovementOperation,
                request.IdempotencyKey,
                requestHashInput,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<CashMovementDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<CashMovementDto, object>(
            request.OrganizationId,
            branchId,
            CashMovementOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<ShiftDto>> CloseShiftAsync(
        Guid shiftId,
        Guid actorStaffUserId,
        CloseShiftRequest request,
        CancellationToken cancellationToken)
    {
        var requestHashInput = ShiftScopedRequest(shiftId, request);
        var shiftBranch = await GetShiftBranchAsync(request.OrganizationId, shiftId, cancellationToken);
        var branchId = shiftBranch ?? Guid.Empty;
        var idempotency = await GetExistingIdempotencyAsync<ShiftDto, object>(
            request.OrganizationId,
            branchId,
            ShiftCloseOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (shiftBranch is null)
        {
            return BillingCommandServiceResult<ShiftDto>.Missing("Shift was not found.");
        }

        if (request.CountedCash.MinorUnits < 0)
        {
            return BillingCommandServiceResult<ShiftDto>.Invalid("Counted cash amount cannot be negative.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var shift = await dbContext.Shifts
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == request.OrganizationId &&
                        candidate.ShiftId == shiftId,
                    cancellationToken);

            if (shift is null)
            {
                return BillingCommandServiceResult<ShiftDto>.Missing("Shift was not found.");
            }

            if (shift.State != ShiftStateNames.Open)
            {
                return BillingCommandServiceResult<ShiftDto>.Invalid("Shift is already closed.");
            }

            if (!string.Equals(shift.CurrencyCode, request.CountedCash.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return BillingCommandServiceResult<ShiftDto>.Invalid("Counted cash currency must match the shift currency.");
            }

            var cashMovementDelta = await dbContext.CashMovements
                .Where(movement => movement.ShiftId == shift.ShiftId)
                .SumAsync(
                    movement => movement.MovementType == CashMovementTypeNames.CashIn
                        ? movement.AmountMinorUnits
                        : -movement.AmountMinorUnits,
                    cancellationToken);
            var posCashDelta = await dbContext.Payments
                .Where(payment =>
                    payment.ShiftId == shift.ShiftId &&
                    payment.CurrencyCode == shift.CurrencyCode &&
                    payment.PaymentMethod == PaymentMethodNames.Cash)
                .SumAsync(payment => (long?)payment.AmountMinorUnits, cancellationToken) ?? 0;
            var billingCashDelta = await dbContext.LedgerEntries
                .Where(entry =>
                    entry.ShiftId == shift.ShiftId &&
                    entry.CurrencyCode == shift.CurrencyCode &&
                    (entry.EntryType == LedgerEntryTypeNames.TopUp ||
                     entry.EntryType == LedgerEntryTypeNames.DebtPayment ||
                     entry.EntryType == LedgerEntryTypeNames.ManualCorrection))
                .SumAsync(entry => (long?)entry.AmountMinorUnits, cancellationToken) ?? 0;
            var expectedCash = shift.StartingCashMinorUnits + cashMovementDelta + posCashDelta + billingCashDelta;
            var difference = request.CountedCash.MinorUnits - expectedCash;
            var now = timeProvider.GetUtcNow();

            shift.State = ShiftStateNames.Closed;
            shift.ClosedByStaffUserId = actorStaffUserId;
            shift.CountedCashMinorUnits = request.CountedCash.MinorUnits;
            shift.ExpectedCashMinorUnits = expectedCash;
            shift.DifferenceMinorUnits = difference;
            shift.ClosingNote = request.ClosingNote.Trim();
            shift.ClosedAtUtc = now;

            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToDto(shift);
            AddIdempotencyRecord(
                request.OrganizationId,
                shift.BranchId,
                ShiftCloseOperation,
                request.IdempotencyKey,
                requestHashInput,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<ShiftDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<ShiftDto, object>(
            request.OrganizationId,
            branchId,
            ShiftCloseOperation,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken),
            IsolationLevel.Serializable,
            cancellationToken);
    }

    private async Task<Guid?> GetShiftBranchAsync(Guid organizationId, Guid shiftId, CancellationToken cancellationToken)
    {
        return await dbContext.Shifts
            .AsNoTracking()
            .Where(shift => shift.OrganizationId == organizationId && shift.ShiftId == shiftId)
            .Select(shift => (Guid?)shift.BranchId)
            .SingleOrDefaultAsync(cancellationToken);
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
        CancellationToken cancellationToken)
    {
        return ExecuteInTransactionAsync(action, recoverIdempotencyRaceAsync, isolationLevel: null, cancellationToken);
    }

    private Task<BillingCommandServiceResult<TResponse>> ExecuteInTransactionAsync<TResponse>(
        Func<Task<BillingCommandServiceResult<TResponse>>> action,
        Func<Task<BillingCommandServiceResult<TResponse>?>>? recoverIdempotencyRaceAsync,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        return ExecuteInTransactionAsync(action, recoverIdempotencyRaceAsync, (IsolationLevel?)isolationLevel, cancellationToken);
    }

    private static string HashRequest<TRequest>(TRequest request)
    {
        return BillingCommandIdempotencyKeyHasher.Hash(JsonSerializer.Serialize(request, JsonOptions));
    }

    private static object ShiftScopedRequest<TRequest>(Guid shiftId, TRequest request)
    {
        return new
        {
            ShiftId = shiftId,
            Request = request
        };
    }

    private static ShiftDto ToDto(ShiftEntity shift)
    {
        var countedCash = shift.ClosedAtUtc is null
            ? null
            : new MoneyDto(shift.CurrencyCode, shift.CountedCashMinorUnits);
        var expectedCash = shift.ClosedAtUtc is null
            ? null
            : new MoneyDto(shift.CurrencyCode, shift.ExpectedCashMinorUnits);
        var difference = shift.ClosedAtUtc is null
            ? null
            : new MoneyDto(shift.CurrencyCode, shift.DifferenceMinorUnits);

        return new ShiftDto(
            shift.ShiftId,
            shift.OrganizationId,
            shift.BranchId,
            shift.OpenedByStaffUserId,
            shift.ClosedByStaffUserId,
            shift.State,
            new MoneyDto(shift.CurrencyCode, shift.StartingCashMinorUnits),
            countedCash,
            expectedCash,
            difference,
            shift.OpeningNote,
            shift.ClosingNote,
            shift.OpenedAtUtc,
            shift.ClosedAtUtc);
    }

    private static CashMovementDto ToDto(CashMovementEntity movement)
    {
        return new CashMovementDto(
            movement.CashMovementId,
            movement.OrganizationId,
            movement.BranchId,
            movement.ShiftId,
            movement.CreatedByStaffUserId,
            movement.MovementType,
            new MoneyDto(movement.CurrencyCode, movement.AmountMinorUnits),
            movement.Reason,
            movement.CreatedAtUtc);
    }
}
