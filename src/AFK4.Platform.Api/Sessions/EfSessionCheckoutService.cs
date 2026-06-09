using System.Data;
using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// Settles a session in one action: time charge + attached POS sales become a
/// single bill, paid by one or more split-payment parts, then the PC is locked
/// and one receipt is produced. The whole thing is transactional and idempotent.
/// </summary>
public sealed class EfSessionCheckoutService(
    PlatformDbContext dbContext,
    ISessionBillingService sessionBillingService,
    IReceiptNumberGenerator receiptNumberGenerator,
    IDeviceCommandDispatchService deviceCommandDispatchService,
    IOpenShiftResolver openShiftResolver,
    IBillingOutbox billingOutbox,
    ISessionLifecycleNotifier lifecycleNotifier,
    TimeProvider timeProvider) : ISessionCheckoutService
{
    private const string CheckoutOperation = "session-checkout";
    private const string SessionCheckoutReceiptType = "session_checkout";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] CheckoutableStates =
    [
        SessionStateNames.Active,
        SessionStateNames.Paused,
        SessionStateNames.Ending
    ];

    public async Task<SessionCheckoutResult> CheckoutAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return SessionCheckoutResult.Missing("Session was not found.");
        }

        if (request.OrganizationId != session.OrganizationId)
        {
            return SessionCheckoutResult.Invalid("Organization id does not match the session.");
        }

        var requestHashInput = new { SessionId = sessionId, Request = request };
        var idempotency = await GetExistingIdempotencyAsync(
            session.OrganizationId,
            session.BranchId,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken);
        if (idempotency is not null)
        {
            return idempotency;
        }

        if (!CheckoutableStates.Contains(session.State))
        {
            return SessionCheckoutResult.Invalid("Only an active session can be checked out.");
        }

        if (request.ExpectedVersion is int expectedVersion && expectedVersion != session.Version)
        {
            return SessionCheckoutResult.StaleVersion(session.Version);
        }

        var now = timeProvider.GetUtcNow();
        var (bill, billError) = await ComputeBillAsync(sessionId, now, cancellationToken);
        if (bill is null)
        {
            return SessionCheckoutResult.Invalid(billError ?? "Checkout charge could not be computed.");
        }

        var timeCharge = bill.TimeCharge;
        var posTotal = bill.PosTotal;
        var currency = bill.Currency;
        var grandTotal = bill.GrandTotal;

        var payments = request.Payments ?? [];
        long paidTotal = 0;
        long walletTotal = 0;
        foreach (var part in payments)
        {
            if (!PaymentMethodNames.IsValid(part.PaymentMethod))
            {
                return SessionCheckoutResult.Invalid("Unsupported payment method.");
            }

            if (part.Amount.MinorUnits <= 0)
            {
                return SessionCheckoutResult.Invalid("Payment amounts must be positive.");
            }

            if (!CurrencyEquals(part.Amount.CurrencyCode, currency))
            {
                return SessionCheckoutResult.Invalid("Payment currency must match the bill.");
            }

            paidTotal = checked(paidTotal + part.Amount.MinorUnits);
            if (part.PaymentMethod == PaymentMethodNames.Wallet)
            {
                walletTotal = checked(walletTotal + part.Amount.MinorUnits);
            }
        }

        if (paidTotal != grandTotal)
        {
            return SessionCheckoutResult.Invalid("Split payments must total the grand total.");
        }

        if (walletTotal > 0)
        {
            if (session.PlayerAccountId is null)
            {
                return SessionCheckoutResult.Invalid("Wallet payment requires a player account.");
            }

            var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(
                dbContext,
                session.PlayerAccountId.Value,
                cancellationToken);
            var walletBalance = summary?.WalletBalance.MinorUnits ?? 0;
            if (walletTotal > walletBalance)
            {
                return SessionCheckoutResult.Invalid("Wallet payment exceeds the player's wallet balance.");
            }
        }

        var openShift = await openShiftResolver.GetOpenShiftIdAsync(session.OrganizationId, session.BranchId, cancellationToken);
        if (!openShift.Succeeded)
        {
            return SessionCheckoutResult.Invalid(openShift.Error ?? "An open shift is required.");
        }

        var shiftId = openShift.Response;

        SessionCheckoutResult result;
        try
        {
            result = await ExecuteInTransactionAsync(async () =>
        {
            var trackedSession = await dbContext.Sessions
                .SingleAsync(candidate => candidate.SessionId == sessionId, cancellationToken);
            if (!CheckoutableStates.Contains(trackedSession.State))
            {
                return SessionCheckoutResult.Invalid("Only an active session can be checked out.");
            }

            var salesToSettle = await dbContext.PosSales
                .Where(sale => sale.SessionId == sessionId && UnpaidStates.Contains(sale.State))
                .ToListAsync(cancellationToken);
            var saleLines = new Dictionary<Guid, List<PosSaleLineEntity>>();
            foreach (var sale in salesToSettle)
            {
                var lines = await dbContext.PosSaleLines
                    .Where(line => line.PosSaleId == sale.PosSaleId)
                    .ToListAsync(cancellationToken);
                saleLines[sale.PosSaleId] = lines;

                // Validate stock before mutating anything (an Invalid return commits an empty tx).
                foreach (var line in lines.Where(line => line.TrackStock && !line.AllowNegativeStock))
                {
                    var stockOnHand = await dbContext.StockMovements
                        .Where(movement =>
                            movement.OrganizationId == sale.OrganizationId &&
                            movement.BranchId == sale.BranchId &&
                            movement.ProductId == line.ProductId)
                        .SumAsync(movement => (int?)movement.QuantityDelta, cancellationToken) ?? 0;
                    if (stockOnHand - line.Quantity < 0)
                    {
                        return SessionCheckoutResult.Invalid("Insufficient stock for tracked product.");
                    }
                }
            }

            // Time charge: create the postpaid debt, then settle it (nets to zero at checkout).
            if (timeCharge.AmountMinorUnits > 0 && trackedSession.PlayerAccountId is Guid timePlayerId)
            {
                await sessionBillingService.AppendCheckoutLedgerEntriesAsync(
                    sessionId,
                    actorStaffUserId,
                    timeCharge,
                    timePlayerId,
                    now,
                    cancellationToken);
                dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
                    trackedSession.OrganizationId,
                    trackedSession.BranchId,
                    timePlayerId,
                    sessionId,
                    playerPackageId: null,
                    LedgerEntryTypeNames.DebtPayment,
                    LedgerAccountTypeNames.Debt,
                    -timeCharge.AmountMinorUnits,
                    quantitySeconds: 0,
                    currency,
                    LedgerEntryTypeNames.DebtPayment,
                    "session checkout time settlement",
                    reversesLedgerEntryId: null,
                    actorStaffUserId,
                    now,
                    shiftId));
            }

            // Wallet store credit consumed to fund the bill.
            if (walletTotal > 0 && trackedSession.PlayerAccountId is Guid walletPlayerId)
            {
                dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
                    trackedSession.OrganizationId,
                    trackedSession.BranchId,
                    walletPlayerId,
                    sessionId,
                    playerPackageId: null,
                    LedgerEntryTypeNames.WalletPayment,
                    LedgerAccountTypeNames.Wallet,
                    -walletTotal,
                    quantitySeconds: 0,
                    currency,
                    LedgerEntryTypeNames.WalletPayment,
                    "wallet applied to session checkout",
                    reversesLedgerEntryId: null,
                    actorStaffUserId,
                    now,
                    shiftId));
            }

            // Settle attached POS sales: decrement stock and mark paid.
            foreach (var sale in salesToSettle)
            {
                foreach (var line in saleLines[sale.PosSaleId].Where(line => line.TrackStock))
                {
                    dbContext.StockMovements.Add(new StockMovementEntity
                    {
                        StockMovementId = Guid.NewGuid(),
                        OrganizationId = sale.OrganizationId,
                        BranchId = sale.BranchId,
                        ProductId = line.ProductId,
                        MovementType = StockMovementTypeNames.Sale,
                        QuantityDelta = -line.Quantity,
                        CurrencyCode = line.CurrencyCode,
                        UnitCostMinorUnits = line.UnitPriceMinorUnits,
                        Reason = $"Session checkout {sessionId}",
                        CreatedByStaffUserId = actorStaffUserId,
                        CreatedAtUtc = now
                    });
                }

                sale.State = PosSaleStateNames.Paid;
                sale.PaidAtUtc = now;
            }

            // Record each payment part against the session.
            foreach (var part in payments)
            {
                dbContext.Payments.Add(new PaymentEntity
                {
                    PaymentId = Guid.NewGuid(),
                    OrganizationId = trackedSession.OrganizationId,
                    BranchId = trackedSession.BranchId,
                    PosSaleId = null,
                    SessionId = sessionId,
                    ShiftId = shiftId,
                    CreatedByStaffUserId = actorStaffUserId,
                    PaymentKind = "payment",
                    Provider = part.PaymentMethod == PaymentMethodNames.Wallet ? "wallet" : "manual",
                    PaymentMethod = part.PaymentMethod,
                    CurrencyCode = currency,
                    AmountMinorUnits = part.Amount.MinorUnits,
                    Note = "session checkout",
                    CreatedAtUtc = now
                });
            }

            var receiptNumber = await receiptNumberGenerator.GenerateAsync(
                trackedSession.OrganizationId,
                trackedSession.BranchId,
                SessionCheckoutReceiptType,
                now,
                cancellationToken);
            var receipt = new ReceiptEntity
            {
                ReceiptId = Guid.NewGuid(),
                OrganizationId = trackedSession.OrganizationId,
                BranchId = trackedSession.BranchId,
                PosSaleId = null,
                SessionId = sessionId,
                ReceiptNumber = receiptNumber,
                ReceiptType = SessionCheckoutReceiptType,
                CurrencyCode = currency,
                TotalMinorUnits = grandTotal,
                CreatedAtUtc = now
            };
            dbContext.Receipts.Add(receipt);

            // End the session and lock the device (mirrors the end flow).
            trackedSession.State = SessionStateNames.Ending;
            trackedSession.UpdatedAtUtc = now;
            trackedSession.Version += 1;
            dbContext.SessionEvents.Add(new SessionEventEntity
            {
                SessionEventId = Guid.NewGuid(),
                SessionId = trackedSession.SessionId,
                OrganizationId = trackedSession.OrganizationId,
                BranchId = trackedSession.BranchId,
                EventType = "session-checkout",
                ActorStaffUserId = actorStaffUserId,
                DeviceId = trackedSession.DeviceId,
                CreatedAtUtc = now,
                DetailsJson = "{}"
            });
            await dbContext.SaveChangesAsync(cancellationToken);

            var command = await deviceCommandDispatchService.EnqueueAsync(
                trackedSession.DeviceId,
                new CreateDeviceCommandRequest(
                    Type: DeviceCommandTypeNames.Lock,
                    Payload: new Dictionary<string, string>
                    {
                        ["sessionId"] = trackedSession.SessionId.ToString("D"),
                        ["reason"] = "session-checkout"
                    }),
                cancellationToken);

            // The realtime wake-up for the lock command rides the billing outbox so it commits in the
            // same transaction as the charge: a committed checkout can never be left un-notified, and a
            // crash can never push a lock for an uncommitted charge. Redelivery is harmless (idempotent
            // key per session) because the command row is itself durable and the push is just a nudge.
            billingOutbox.Enqueue(new OutboxMessageEntity
            {
                OutboxMessageId = Guid.NewGuid(),
                OrganizationId = trackedSession.OrganizationId,
                BranchId = trackedSession.BranchId,
                Type = OutboxMessageTypes.SessionCheckout,
                PayloadJson = JsonSerializer.Serialize(
                    new SessionCheckoutNotifyPayload(trackedSession.DeviceId, command),
                    JsonOptions),
                Status = OutboxMessageStatus.Pending,
                AvailableAtUtc = now,
                CreatedAtUtc = now,
                IdempotencyKey = $"session-checkout-notify:{trackedSession.SessionId:D}"
            });

            var response = new SessionCheckoutResponse(
                IdempotencyKey: request.IdempotencyKey,
                SessionId: trackedSession.SessionId,
                TimeCharge: new MoneyDto(currency, timeCharge.AmountMinorUnits),
                PosTotal: new MoneyDto(currency, posTotal),
                GrandTotal: new MoneyDto(currency, grandTotal),
                Payments: payments,
                Receipt: ToDto(receipt),
                Session: ToSessionDto(trackedSession, now),
                DeviceCommands: [command]);

            AddIdempotencyRecord(
                trackedSession.OrganizationId,
                trackedSession.BranchId,
                request.IdempotencyKey,
                requestHashInput,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return SessionCheckoutResult.Ok(response);
        }, () => ReplayIdempotencyAsync(
            session.OrganizationId,
            session.BranchId,
            request.IdempotencyKey,
            requestHashInput,
            cancellationToken), cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another mutation bumped the version between our snapshot and commit: the loser
            // gets a typed 409 with the now-current version instead of a phantom double-checkout.
            dbContext.ChangeTracker.Clear();
            var currentVersion = await dbContext.Sessions
                .AsNoTracking()
                .Where(candidate => candidate.SessionId == sessionId)
                .Select(candidate => (int?)candidate.Version)
                .SingleOrDefaultAsync(cancellationToken) ?? 0;
            return SessionCheckoutResult.StaleVersion(currentVersion);
        }

        if (result.Succeeded && result.Response is not null)
        {
            var settled = result.Response.Session;
            await lifecycleNotifier.NotifyAsync(
                new SessionLifecycleChangedDto(
                    OrganizationId: settled.OrganizationId,
                    BranchId: settled.BranchId,
                    SeatId: settled.SeatId,
                    SessionId: settled.SessionId,
                    Kind: SessionLifecycleKinds.Checkout,
                    State: settled.State,
                    Version: settled.Version,
                    StartedAtUtc: settled.StartedAtUtc,
                    EndsAtUtc: settled.EndsAtUtc,
                    ObservedAtUtc: timeProvider.GetUtcNow(),
                    AccruedCostMinorUnits: result.Response.GrandTotal.MinorUnits,
                    CurrencyCode: result.Response.GrandTotal.CurrencyCode),
                cancellationToken);
        }

        return result;
    }

    public async Task<SessionCheckoutQuoteResult> QuoteAsync(
        Guid sessionId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);
        if (session is null)
        {
            return SessionCheckoutQuoteResult.Missing("Session was not found.");
        }

        if (organizationId != session.OrganizationId)
        {
            return SessionCheckoutQuoteResult.Invalid("Organization id does not match the session.");
        }

        if (!CheckoutableStates.Contains(session.State))
        {
            return SessionCheckoutQuoteResult.Invalid("Only an active session can be checked out.");
        }

        var now = timeProvider.GetUtcNow();
        var (bill, billError) = await ComputeBillAsync(sessionId, now, cancellationToken);
        if (bill is null)
        {
            return SessionCheckoutQuoteResult.Invalid(billError ?? "Checkout charge could not be computed.");
        }

        MoneyDto? walletBalance = null;
        if (session.PlayerAccountId is Guid playerId)
        {
            var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(dbContext, playerId, cancellationToken);
            walletBalance = new MoneyDto(bill.Currency, summary?.WalletBalance.MinorUnits ?? 0);
        }

        var response = new SessionCheckoutQuoteResponse(
            SessionId: sessionId,
            TimeCharge: new MoneyDto(bill.Currency, bill.TimeCharge.AmountMinorUnits),
            PosTotal: new MoneyDto(bill.Currency, bill.PosTotal),
            GrandTotal: new MoneyDto(bill.Currency, bill.GrandTotal),
            BillableSeconds: bill.TimeCharge.BillableSeconds,
            PlayerAccountId: session.PlayerAccountId,
            WalletBalance: walletBalance);
        return SessionCheckoutQuoteResult.Ok(response);
    }

    private sealed record Bill(
        SessionBillingValidationResult TimeCharge,
        long PosTotal,
        string Currency,
        long GrandTotal);

    /// <summary>
    /// Computes the unified bill (time charge + attached unpaid POS) in a single
    /// currency. Shared by the quote preview and the settling checkout so the two
    /// never disagree. Returns a human-readable error instead of a bill on failure.
    /// </summary>
    private async Task<(Bill? Bill, string? Error)> ComputeBillAsync(
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var timeCharge = await sessionBillingService.ComputeCheckoutChargeAsync(sessionId, now, cancellationToken);
        if (!timeCharge.Succeeded)
        {
            return (null, timeCharge.Error ?? "Checkout charge could not be computed.");
        }

        var attachedSales = await dbContext.PosSales
            .AsNoTracking()
            .Where(sale => sale.SessionId == sessionId && UnpaidStates.Contains(sale.State))
            .ToListAsync(cancellationToken);
        var posTotal = attachedSales.Sum(sale => sale.TotalMinorUnits);

        // Resolve the single bill currency across the time charge and POS tab.
        string? currency = timeCharge.AmountMinorUnits > 0 ? timeCharge.CurrencyCode : null;
        foreach (var sale in attachedSales)
        {
            if (currency is null)
            {
                currency = sale.CurrencyCode;
            }
            else if (!CurrencyEquals(currency, sale.CurrencyCode))
            {
                return (null, "Checkout cannot mix currencies.");
            }
        }

        currency ??= timeCharge.CurrencyCode;
        currency = currency.ToUpperInvariant();

        long grandTotal;
        try
        {
            grandTotal = checked(timeCharge.AmountMinorUnits + posTotal);
        }
        catch (OverflowException)
        {
            return (null, "Checkout total is too large.");
        }

        return (new Bill(timeCharge, posTotal, currency, grandTotal), null);
    }

    private static readonly string[] UnpaidStates =
    [
        PosSaleStateNames.Draft,
        PosSaleStateNames.PendingPayment
    ];

    private static bool CurrencyEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static ReceiptDto ToDto(ReceiptEntity receipt) =>
        new(
            receipt.ReceiptId,
            receipt.OrganizationId,
            receipt.BranchId,
            receipt.PosSaleId,
            receipt.ReceiptNumber,
            receipt.ReceiptType,
            new MoneyDto(receipt.CurrencyCode, receipt.TotalMinorUnits),
            receipt.CreatedAtUtc,
            receipt.SessionId);

    private static SessionDto ToSessionDto(SessionEntity session, DateTimeOffset now) =>
        new(
            SessionId: session.SessionId,
            OrganizationId: session.OrganizationId,
            BranchId: session.BranchId,
            SeatId: session.SeatId,
            DeviceId: session.DeviceId,
            State: session.State,
            TariffRuleVersionId: session.TariffRuleVersionId,
            StartedAtUtc: session.StartedAtUtc,
            EndsAtUtc: session.EndsAtUtc,
            EndedAtUtc: session.EndedAtUtc,
            RemainingSeconds: session.EndsAtUtc is null
                ? null
                : Math.Max(0, (int)(session.EndsAtUtc.Value - now).TotalSeconds),
            CurrentLease: null,
            Version: session.Version);

    private async Task<SessionCheckoutResult?> GetExistingIdempotencyAsync(
        Guid organizationId,
        Guid branchId,
        string idempotencyKey,
        object request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return SessionCheckoutResult.Invalid("Idempotency key is required.");
        }

        var requestHash = HashRequest(request);
        var idempotencyKeyHash = BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey);
        var existing = await dbContext.BillingCommandIdempotency
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == branchId &&
                    candidate.Operation == CheckoutOperation &&
                    candidate.IdempotencyKeyHash == idempotencyKeyHash,
                cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return SessionCheckoutResult.RequestConflict("Idempotency key was already used for a different checkout.");
        }

        var response = JsonSerializer.Deserialize<SessionCheckoutResponse>(existing.ResponseJson, JsonOptions);
        return response is null
            ? SessionCheckoutResult.Invalid("Stored idempotent response could not be read.")
            : SessionCheckoutResult.Ok(response);
    }

    private void AddIdempotencyRecord(
        Guid organizationId,
        Guid branchId,
        string idempotencyKey,
        object request,
        SessionCheckoutResponse response,
        DateTimeOffset now)
    {
        dbContext.BillingCommandIdempotency.Add(new BillingCommandIdempotencyEntity
        {
            BillingCommandIdempotencyId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            Operation = CheckoutOperation,
            IdempotencyKeyHash = BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey),
            RequestHash = HashRequest(request),
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1)
        });
    }

    private async Task<SessionCheckoutResult?> ReplayIdempotencyAsync(
        Guid organizationId,
        Guid branchId,
        string idempotencyKey,
        object request,
        CancellationToken cancellationToken)
    {
        return await GetExistingIdempotencyAsync(organizationId, branchId, idempotencyKey, request, cancellationToken);
    }

    private async Task<SessionCheckoutResult> ExecuteInTransactionAsync(
        Func<Task<SessionCheckoutResult>> action,
        Func<Task<SessionCheckoutResult?>> recoverIdempotencyRaceAsync,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            try
            {
                return await action();
            }
            catch (DbUpdateException)
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException)
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

    private static string HashRequest(object request) =>
        BillingCommandIdempotencyKeyHasher.Hash(JsonSerializer.Serialize(request, JsonOptions));
}
