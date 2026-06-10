# Shift Revenue Screen — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Shifts" screen to the operator web app showing per-shift revenue — what was earned (session time + POS goods) and what money came in (cash / non-cash / wallet top-ups) — for the current open shift plus a history of recent shifts.

**Architecture:** A new read-only aggregate `GetShiftRevenueAsync` in the existing `EfReportService`, over data already tagged by `ShiftId` (ledger entries, payments, POS sales, shift cash fields). New DTOs in `AFK4.Shared.Contracts/Shifts`, two new GET endpoints in `ShiftEndpoints.cs`, a typed web client, and a `ShiftsWorkspace.tsx` React screen wired into nav/permissions/i18n. Cash reconciliation reuses the existing shift cash computation. Shifts are intervals (open→close) and thus timezone-immune.

**Tech Stack:** .NET 10 (minimal APIs, EF Core, xUnit), React + TypeScript (Vite, bun test, @afk4/i18n), lucide-react icons.

**Money semantics (verified in code — do not re-derive):**
- `GameplayCharge` ledger amount is **negative** (wallet debit); `PostpaidDebt` is **positive**. Both carry the open shift's `ShiftId`.
- `earned.time = −Σ GameplayCharge.amount + Σ PostpaidDebt.amount` for the shift.
- `earned.goods = Σ PosSale.TotalMinorUnits` where `PaidAtUtc != null && VoidedAtUtc == null && RefundedAtUtc == null`.
- `inflow.cash` / `inflow.nonCash = Σ Payments(kind=payment) − Σ Payments(kind=refund)` filtered by `PaymentMethod` (`cash`, `card_manual`). `wallet` payments are **excluded** (internal transfer; money already arrived at top-up).
- `inflow.walletTopUps = Σ ledger TopUp.amount` for the shift (no method split).
- `PackageConsumption` / `BonusConsumption` are `amount = 0` → not counted. Packages out of MVP.
- All values in the shift's `CurrencyCode`; non-matching currencies ignored (use the existing `IsCurrency` helper).

---

## File Structure

**Backend (create):**
- `src/AFK4.Shared.Contracts/Shifts/ShiftRevenueDto.cs` — the revenue DTO + nested breakdown records.

**Backend (modify):**
- `src/AFK4.Platform.Api/Reports/IReportService.cs` — add `GetShiftRevenueAsync` + `GetCurrentShiftRevenueAsync`.
- `src/AFK4.Platform.Api/Reports/EfReportService.cs` — implement both.
- `src/AFK4.Platform.Api/Endpoints/ShiftEndpoints.cs` — two GET endpoints.

**Backend (test):**
- `tests/AFK4.Platform.Api.Tests/ShiftRevenueReportTests.cs` — new test file (mirrors `EfReportServiceTests.cs` helpers).

**Frontend (modify):**
- `src/AFK4.Operator.App.Web/src/operatorApiClients.ts` — `ShiftRevenueDto` TS types + `createShiftRevenueClient` + wire into `createOperatorApiClients`.
- `src/AFK4.Operator.App.Web/src/operatorPermissions.ts` — add `'shifts'` workspace id + rule.
- `src/AFK4.Operator.App.Web/src/operatorData.ts` — add nav item.
- `src/AFK4.Operator.App.Web/src/App.tsx` — render `ShiftsWorkspace`.
- `packages/i18n/locales/ru.json`, `en.json`, `tg.json` — keys.

**Frontend (create):**
- `src/AFK4.Operator.App.Web/src/ShiftsWorkspace.tsx`
- `src/AFK4.Operator.App.Web/src/ShiftsWorkspace.test.tsx`

---

## Task 1: Revenue DTOs (Shared.Contracts)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Shifts/ShiftRevenueDto.cs`

- [ ] **Step 1: Create the DTO file**

```csharp
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shifts;

public sealed record EarnedBreakdownDto(MoneyDto Time, MoneyDto Goods, MoneyDto Total);

public sealed record InflowBreakdownDto(MoneyDto Cash, MoneyDto NonCash, MoneyDto WalletTopUps, MoneyDto DirectTotal);

public sealed record CashReconciliationDto(MoneyDto Starting, MoneyDto Expected, MoneyDto? Counted, MoneyDto? Difference);

public sealed record ShiftRevenueDto(
    Guid ShiftId,
    Guid OrganizationId,
    Guid BranchId,
    Guid OpenedByStaffUserId,
    Guid? ClosedByStaffUserId,
    string State,
    EarnedBreakdownDto Earned,
    InflowBreakdownDto Inflow,
    CashReconciliationDto Cash,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc);

public sealed record ShiftRevenueListDto(IReadOnlyList<ShiftRevenueDto> Shifts, int Limit);
```

- [ ] **Step 2: Build the contracts project**

Run: `dotnet build src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj --nologo`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Shared.Contracts/Shifts/ShiftRevenueDto.cs
git commit -m "feat(contracts): add shift revenue DTOs"
```

---

## Task 2: Backend aggregate `GetShiftRevenueAsync` (TDD)

**Files:**
- Modify: `src/AFK4.Platform.Api/Reports/IReportService.cs`
- Modify: `src/AFK4.Platform.Api/Reports/EfReportService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/ShiftRevenueReportTests.cs`

> **Pattern reference:** copy the in-memory DbContext setup and seed-helper style from `tests/AFK4.Platform.Api.Tests/EfReportServiceTests.cs`. `CreateDbContext()` uses `.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))`. `EfReportService` is constructed as `new EfReportService(db)`.

- [ ] **Step 1: Add interface methods**

In `IReportService.cs`, add:

```csharp
Task<ShiftRevenueListDto> GetShiftRevenueAsync(
    Guid organizationId, Guid branchId, ReportSearchQuery query, CancellationToken cancellationToken);

Task<ShiftRevenueDto?> GetCurrentShiftRevenueAsync(
    Guid organizationId, Guid branchId, CancellationToken cancellationToken);
```

Add `using AFK4.Shared.Contracts.Shifts;` if not present.

- [ ] **Step 2: Write the failing test (full revenue aggregation)**

Create `tests/AFK4.Platform.Api.Tests/ShiftRevenueReportTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class ShiftRevenueReportTests
{
    private static readonly Guid OrgId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid StaffId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly DateTimeOffset Opened = DateTimeOffset.Parse("2026-06-10T09:00:00Z");
    private const string Tjs = "TJS";

    private static PlatformDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public async Task GetCurrentShiftRevenue_AggregatesEarnedAndInflow()
    {
        await using var db = CreateDbContext();
        var shiftId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        SeedOpenShift(db, shiftId, startingCash: 10000);
        SeedLedger(db, shiftId, LedgerEntryTypeNames.GameplayCharge, -3100);     // earned time 3100
        SeedLedger(db, shiftId, LedgerEntryTypeNames.PostpaidDebt, 200);         // earned time +200
        SeedLedger(db, shiftId, LedgerEntryTypeNames.TopUp, 900);                // wallet top-ups 900
        SeedPosSale(db, shiftId, total: 1150, paid: true);                       // earned goods 1150
        SeedPayment(db, shiftId, PaymentMethodNames.Cash, "payment", 2000);      // inflow cash 2000
        SeedPayment(db, shiftId, PaymentMethodNames.CardManual, "payment", 1800);// inflow non-cash 1800
        SeedPayment(db, shiftId, PaymentMethodNames.Wallet, "payment", 500);     // EXCLUDED
        await db.SaveChangesAsync();
        var service = new EfReportService(db);

        var result = await service.GetCurrentShiftRevenueAsync(OrgId, BranchId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3300, result!.Earned.Time.MinorUnits);      // 3100 + 200
        Assert.Equal(1150, result.Earned.Goods.MinorUnits);
        Assert.Equal(4450, result.Earned.Total.MinorUnits);      // 3300 + 1150
        Assert.Equal(2000, result.Inflow.Cash.MinorUnits);
        Assert.Equal(1800, result.Inflow.NonCash.MinorUnits);
        Assert.Equal(900, result.Inflow.WalletTopUps.MinorUnits);
        Assert.Equal(3800, result.Inflow.DirectTotal.MinorUnits); // 2000 + 1800 (wallet excluded)
        Assert.Equal(10000, result.Cash.Starting.MinorUnits);
        Assert.Null(result.Cash.Counted);                         // still open
    }

    [Fact]
    public async Task GetCurrentShiftRevenue_RefundsReduceInflow()
    {
        await using var db = CreateDbContext();
        var shiftId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        SeedOpenShift(db, shiftId, startingCash: 0);
        SeedPayment(db, shiftId, PaymentMethodNames.Cash, "payment", 5000);
        SeedPayment(db, shiftId, PaymentMethodNames.Cash, "refund", 1500);
        await db.SaveChangesAsync();
        var service = new EfReportService(db);

        var result = await service.GetCurrentShiftRevenueAsync(OrgId, BranchId, CancellationToken.None);

        Assert.Equal(3500, result!.Inflow.Cash.MinorUnits);       // 5000 − 1500
    }

    [Fact]
    public async Task GetCurrentShiftRevenue_NoOpenShift_ReturnsNull()
    {
        await using var db = CreateDbContext();
        var service = new EfReportService(db);

        var result = await service.GetCurrentShiftRevenueAsync(OrgId, BranchId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetShiftRevenue_ListsClosedShiftsWithReconciliation()
    {
        await using var db = CreateDbContext();
        var shiftId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        SeedClosedShift(db, shiftId, startingCash: 10000, countedCash: 14000);
        SeedPayment(db, shiftId, PaymentMethodNames.Cash, "payment", 4000);
        await db.SaveChangesAsync();
        var service = new EfReportService(db);

        var result = await service.GetShiftRevenueAsync(
            OrgId, BranchId, new ReportSearchQuery(null, null, 10), CancellationToken.None);

        var row = Assert.Single(result.Shifts);
        Assert.Equal(shiftId, row.ShiftId);
        Assert.Equal(14000, row.Cash.Counted!.MinorUnits);
        // expected = starting(10000) + cash payment(4000) = 14000 → difference 0
        Assert.Equal(14000, row.Cash.Expected.MinorUnits);
        Assert.Equal(0, row.Cash.Difference!.MinorUnits);
    }

    private static void SeedOpenShift(PlatformDbContext db, Guid shiftId, long startingCash) =>
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = shiftId, OrganizationId = OrgId, BranchId = BranchId,
            OpenedByStaffUserId = StaffId, State = ShiftStateNames.Open, CurrencyCode = Tjs,
            StartingCashMinorUnits = startingCash, OpenedAtUtc = Opened
        });

    private static void SeedClosedShift(PlatformDbContext db, Guid shiftId, long startingCash, long countedCash) =>
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = shiftId, OrganizationId = OrgId, BranchId = BranchId,
            OpenedByStaffUserId = StaffId, ClosedByStaffUserId = StaffId,
            State = ShiftStateNames.Closed, CurrencyCode = Tjs,
            StartingCashMinorUnits = startingCash, CountedCashMinorUnits = countedCash,
            OpenedAtUtc = Opened, ClosedAtUtc = Opened.AddHours(8)
        });

    private static void SeedLedger(PlatformDbContext db, Guid shiftId, string entryType, long amount) =>
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId,
            ShiftId = shiftId, PlayerAccountId = Guid.NewGuid(), EntryType = entryType,
            AccountType = "wallet", AmountMinorUnits = amount, CurrencyCode = Tjs,
            CreatedByStaffUserId = StaffId, CreatedAtUtc = Opened.AddHours(1)
        });

    private static void SeedPosSale(PlatformDbContext db, Guid shiftId, long total, bool paid) =>
        db.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId,
            ShiftId = shiftId, CreatedByStaffUserId = StaffId, State = "paid",
            CurrencyCode = Tjs, TotalMinorUnits = total,
            CreatedAtUtc = Opened.AddHours(1), PaidAtUtc = paid ? Opened.AddHours(1) : null
        });

    private static void SeedPayment(PlatformDbContext db, Guid shiftId, string method, string kind, long amount) =>
        db.Payments.Add(new PaymentEntity
        {
            PaymentId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId,
            ShiftId = shiftId, CreatedByStaffUserId = StaffId, PaymentKind = kind,
            PaymentMethod = method, CurrencyCode = Tjs, AmountMinorUnits = amount,
            CreatedAtUtc = Opened.AddHours(1)
        });
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~ShiftRevenueReportTests" --nologo`
Expected: FAIL — `GetShiftRevenueAsync` / `GetCurrentShiftRevenueAsync` not implemented.

- [ ] **Step 4: Implement the aggregate in `EfReportService.cs`**

Add `using AFK4.Shared.Contracts.Shifts;` at the top. Add these methods to the class (place near `GetShiftReportAsync`). They reuse existing `Money`, `IsCurrency`, `NormalizeLimit`, `PaymentKindPayment`, `PaymentKindRefund` members.

```csharp
public async Task<ShiftRevenueDto?> GetCurrentShiftRevenueAsync(
    Guid organizationId, Guid branchId, CancellationToken cancellationToken)
{
    var shift = await dbContext.Shifts
        .AsNoTracking()
        .Where(s => s.OrganizationId == organizationId && s.BranchId == branchId && s.State == ShiftStateNames.Open)
        .OrderByDescending(s => s.OpenedAtUtc)
        .FirstOrDefaultAsync(cancellationToken);

    return shift is null ? null : await BuildShiftRevenueAsync(shift, cancellationToken);
}

public async Task<ShiftRevenueListDto> GetShiftRevenueAsync(
    Guid organizationId, Guid branchId, ReportSearchQuery query, CancellationToken cancellationToken)
{
    var limit = NormalizeLimit(query.Limit);
    var shiftsQuery = dbContext.Shifts
        .AsNoTracking()
        .Where(s => s.OrganizationId == organizationId && s.BranchId == branchId);

    if (query.FromUtc is { } fromUtc)
    {
        shiftsQuery = shiftsQuery.Where(s => s.OpenedAtUtc >= fromUtc);
    }

    if (query.ToUtc is { } toUtc)
    {
        shiftsQuery = shiftsQuery.Where(s => s.OpenedAtUtc <= toUtc);
    }

    var shifts = await shiftsQuery
        .OrderByDescending(s => s.OpenedAtUtc)
        .Take(limit)
        .ToListAsync(cancellationToken);

    var rows = new List<ShiftRevenueDto>(shifts.Count);
    foreach (var shift in shifts)
    {
        rows.Add(await BuildShiftRevenueAsync(shift, cancellationToken));
    }

    return new ShiftRevenueListDto(rows, limit);
}

private async Task<ShiftRevenueDto> BuildShiftRevenueAsync(ShiftEntity shift, CancellationToken cancellationToken)
{
    var currency = shift.CurrencyCode;

    var ledger = await dbContext.LedgerEntries
        .AsNoTracking()
        .Where(e => e.ShiftId == shift.ShiftId && e.OrganizationId == shift.OrganizationId)
        .ToListAsync(cancellationToken);
    var payments = await dbContext.Payments
        .AsNoTracking()
        .Where(p => p.ShiftId == shift.ShiftId && p.OrganizationId == shift.OrganizationId)
        .ToListAsync(cancellationToken);
    var sales = await dbContext.PosSales
        .AsNoTracking()
        .Where(s => s.ShiftId == shift.ShiftId && s.OrganizationId == shift.OrganizationId)
        .ToListAsync(cancellationToken);
    var cashMovements = await dbContext.CashMovements
        .AsNoTracking()
        .Where(m => m.ShiftId == shift.ShiftId && m.OrganizationId == shift.OrganizationId)
        .ToListAsync(cancellationToken);

    bool Cur(string code) => IsCurrency(code, currency);

    var earnedTime =
        -ledger.Where(e => e.EntryType == LedgerEntryTypeNames.GameplayCharge && Cur(e.CurrencyCode))
               .Sum(e => e.AmountMinorUnits)
        + ledger.Where(e => e.EntryType == LedgerEntryTypeNames.PostpaidDebt && Cur(e.CurrencyCode))
                .Sum(e => e.AmountMinorUnits);
    var earnedGoods = sales
        .Where(s => Cur(s.CurrencyCode) && s.PaidAtUtc != null && s.VoidedAtUtc == null && s.RefundedAtUtc == null)
        .Sum(s => s.TotalMinorUnits);

    long MethodNet(string method) =>
        payments.Where(p => p.PaymentMethod == method && Cur(p.CurrencyCode) && p.PaymentKind == PaymentKindPayment).Sum(p => p.AmountMinorUnits)
        - payments.Where(p => p.PaymentMethod == method && Cur(p.CurrencyCode) && p.PaymentKind == PaymentKindRefund).Sum(p => p.AmountMinorUnits);

    var cash = MethodNet(PaymentMethodNames.Cash);
    var nonCash = MethodNet(PaymentMethodNames.CardManual);
    var walletTopUps = ledger
        .Where(e => e.EntryType == LedgerEntryTypeNames.TopUp && Cur(e.CurrencyCode))
        .Sum(e => e.AmountMinorUnits);

    // Cash reconciliation mirrors GetShiftReportAsync.
    var cashMovementTotal = cashMovements
        .Where(m => Cur(m.CurrencyCode))
        .Sum(m => m.MovementType == CashMovementTypeNames.CashIn ? m.AmountMinorUnits : -m.AmountMinorUnits);
    var posCashPayments = payments
        .Where(p => p.PaymentMethod == PaymentMethodNames.Cash && Cur(p.CurrencyCode) && p.PaymentKind == PaymentKindPayment)
        .Sum(p => p.AmountMinorUnits);
    var posCashRefunds = payments
        .Where(p => p.PaymentMethod == PaymentMethodNames.Cash && Cur(p.CurrencyCode) && p.PaymentKind == PaymentKindRefund)
        .Sum(p => -p.AmountMinorUnits);
    var billingCashImpact = ledger
        .Where(e => Cur(e.CurrencyCode) &&
            (e.EntryType == LedgerEntryTypeNames.TopUp ||
             e.EntryType == LedgerEntryTypeNames.DebtPayment ||
             e.EntryType == LedgerEntryTypeNames.ManualCorrection))
        .Sum(e => e.EntryType == LedgerEntryTypeNames.DebtPayment ? -e.AmountMinorUnits : e.AmountMinorUnits);
    var expectedCash = shift.StartingCashMinorUnits + cashMovementTotal + posCashPayments + posCashRefunds + billingCashImpact;
    var isClosed = shift.State == ShiftStateNames.Closed;

    return new ShiftRevenueDto(
        shift.ShiftId,
        shift.OrganizationId,
        shift.BranchId,
        shift.OpenedByStaffUserId,
        shift.ClosedByStaffUserId,
        shift.State,
        new EarnedBreakdownDto(Money(currency, earnedTime), Money(currency, earnedGoods), Money(currency, earnedTime + earnedGoods)),
        new InflowBreakdownDto(Money(currency, cash), Money(currency, nonCash), Money(currency, walletTopUps), Money(currency, cash + nonCash)),
        new CashReconciliationDto(
            Money(currency, shift.StartingCashMinorUnits),
            Money(currency, expectedCash),
            isClosed ? Money(currency, shift.CountedCashMinorUnits) : null,
            isClosed ? Money(currency, shift.CountedCashMinorUnits - expectedCash) : null),
        shift.OpenedAtUtc,
        shift.ClosedAtUtc);
}
```

> Note: confirm the exact `using` for `PaymentMethodNames` (`AFK4.Shared.Contracts.Payments`) and `CashMovementTypeNames` are already imported in `EfReportService.cs` (the file already references `CashMovementTypeNames` and `PaymentMethodNames` in `GetShiftReportAsync`, so they are).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~ShiftRevenueReportTests" --nologo`
Expected: PASS — 4 tests.

- [ ] **Step 6: Run the full API test suite (no regressions)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --nologo`
Expected: PASS — all green.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Api/Reports/IReportService.cs src/AFK4.Platform.Api/Reports/EfReportService.cs tests/AFK4.Platform.Api.Tests/ShiftRevenueReportTests.cs
git commit -m "feat(reports): add shift revenue aggregate (earned + inflow + cash recon)"
```

---

## Task 3: HTTP endpoints

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/ShiftEndpoints.cs`

> **Pattern reference:** the shift-report endpoint in `src/AFK4.Platform.Api/Endpoints/ReportEndpoints.cs` (`GET /api/branches/{branchId:guid}/reports/shifts`) shows the exact auth + `WriteAuditAsync` + `ReportSearchQuery` shape. Reuse `StaffPermissionNames.ViewReports` and `AuditActionNames.ViewShiftReport`.

- [ ] **Step 1: Add the two endpoints**

Inside `MapShiftEndpoints`, after the existing `current` endpoint, add:

```csharp
app.MapGet("/api/branches/{branchId:guid}/shifts/revenue/current", async (
    Guid branchId,
    StaffAuthorizationService authorizationService,
    IReportService reportService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId, StaffPermissionNames.ViewReports, cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await reportService.GetCurrentShiftRevenueAsync(
        authorization.StaffContext!.OrganizationId, branchId, cancellationToken);

    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapGet("/api/branches/{branchId:guid}/shifts/revenue", async (
    Guid branchId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? limit,
    StaffAuthorizationService authorizationService,
    IReportService reportService,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId, StaffPermissionNames.ViewReports, cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var query = new ReportSearchQuery(fromUtc, toUtc, limit);
    var result = await reportService.GetShiftRevenueAsync(
        authorization.StaffContext!.OrganizationId, branchId, query, cancellationToken);

    return Results.Ok(result);
});
```

Ensure `using AFK4.Platform.Api.Reports;` and `using AFK4.Shared.Contracts.Reports;` are present (add if missing).

- [ ] **Step 2: Build the API**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --nologo`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Platform.Api/Endpoints/ShiftEndpoints.cs
git commit -m "feat(api): add shift revenue endpoints (current + history)"
```

---

## Task 4: Operator web API client

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`

> **Pattern reference:** `createNewsClient` + `createShiftClient` in this same file, and the `news:`/`shifts:` wiring inside `createOperatorApiClients`.

- [ ] **Step 1: Add TS types + client factory**

Near the other client factories in `operatorApiClients.ts`, add:

```typescript
export interface MoneyDto {
  currencyCode: string;
  minorUnits: number;
}

export interface ShiftRevenueDto {
  shiftId: string;
  organizationId: string;
  branchId: string;
  openedByStaffUserId: string;
  closedByStaffUserId: string | null;
  state: string;
  earned: { time: MoneyDto; goods: MoneyDto; total: MoneyDto };
  inflow: { cash: MoneyDto; nonCash: MoneyDto; walletTopUps: MoneyDto; directTotal: MoneyDto };
  cash: { starting: MoneyDto; expected: MoneyDto; counted: MoneyDto | null; difference: MoneyDto | null };
  openedAtUtc: string;
  closedAtUtc: string | null;
}

export interface ShiftRevenueListDto {
  shifts: ShiftRevenueDto[];
  limit: number;
}

export function createShiftRevenueClient(api: PlatformApiClient) {
  return {
    current(branchId: Guid): Promise<ShiftRevenueDto | null> {
      return api.getOptional<ShiftRevenueDto>(`/api/branches/${branchId}/shifts/revenue/current`);
    },
    history(branchId: Guid, limit = 20): Promise<ShiftRevenueListDto> {
      return api.get<ShiftRevenueListDto>(`/api/branches/${branchId}/shifts/revenue`, { limit });
    }
  };
}
```

> If `MoneyDto` is already declared in this file, do not redeclare it — reuse the existing one.

- [ ] **Step 2: Wire into `createOperatorApiClients`**

In the object returned by `createOperatorApiClients`, add:

```typescript
    shiftRevenue: createShiftRevenueClient(api),
```

- [ ] **Step 3: Typecheck**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: tsc + vite build succeed, 0 type errors.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorApiClients.ts
git commit -m "feat(operator-web): add shift revenue api client"
```

---

## Task 5: `ShiftsWorkspace` component (TDD)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/ShiftsWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/ShiftsWorkspace.test.tsx`

> **Pattern reference:** `NewsWorkspace.tsx` for the `backend` prop + injectable `client` + `useI18n` shape, and `NewsWorkspace.test.tsx` for the mocked-client render test. The component takes an injectable `client` so the test can pass a fake.

- [ ] **Step 1: Write the failing component test**

Create `src/AFK4.Operator.App.Web/src/ShiftsWorkspace.test.tsx`:

```typescript
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ShiftsWorkspace } from './ShiftsWorkspace';
import type { ShiftRevenueDto } from './operatorApiClients';

function money(minorUnits: number) {
  return { currencyCode: 'TJS', minorUnits };
}

function shift(overrides: Partial<ShiftRevenueDto> = {}): ShiftRevenueDto {
  return {
    shiftId: 's1', organizationId: 'o1', branchId: 'b1',
    openedByStaffUserId: 'u1', closedByStaffUserId: null, state: 'open',
    earned: { time: money(310000), goods: money(115000), total: money(425000) },
    inflow: { cash: money(200000), nonCash: money(180000), walletTopUps: money(90000), directTotal: money(380000) },
    cash: { starting: money(1000000), expected: money(1380000), counted: null, difference: null },
    openedAtUtc: '2026-06-10T09:00:00Z', closedAtUtc: null,
    ...overrides
  };
}

function client(current: ShiftRevenueDto | null, history: ShiftRevenueDto[] = []) {
  return {
    current: async () => current,
    history: async () => ({ shifts: history, limit: 20 })
  };
}

describe('ShiftsWorkspace', () => {
  it('renders earned and inflow breakdown for the current shift', async () => {
    render(
      <I18nProvider>
        <ShiftsWorkspace backend={null} branchId="b1" client={client(shift()) as never} />
      </I18nProvider>
    );

    await waitFor(() => screen.getByText(/4\s?250/)); // earned total 425000 minor → 4 250
    expect(screen.getByText(/3\s?100/)).toBeTruthy(); // earned time
    expect(screen.getByText(/1\s?150/)).toBeTruthy(); // earned goods
  });

  it('shows an empty state when no shift is open', async () => {
    render(
      <I18nProvider>
        <ShiftsWorkspace backend={null} branchId="b1" client={client(null) as never} />
      </I18nProvider>
    );

    await waitFor(() => screen.getByText(/смен/i));
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test ShiftsWorkspace`
Expected: FAIL — module `./ShiftsWorkspace` not found.

- [ ] **Step 3: Implement the component**

Create `src/AFK4.Operator.App.Web/src/ShiftsWorkspace.tsx`. Format money minor-units → major with a thin space thousands separator. Keep it focused: a current-shift card + a history list.

```typescript
import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import type { OperatorBackendContext } from './operatorTypes';
import type { MoneyDto, ShiftRevenueDto } from './operatorApiClients';

interface ShiftRevenueClient {
  current(branchId: string): Promise<ShiftRevenueDto | null>;
  history(branchId: string, limit?: number): Promise<{ shifts: ShiftRevenueDto[]; limit: number }>;
}

function formatMoney(m: MoneyDto): string {
  const major = (m.minorUnits / 100).toFixed(m.minorUnits % 100 === 0 ? 0 : 2);
  return major.replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
}

export function ShiftsWorkspace({
  backend,
  branchId,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  branchId: string;
  client?: ShiftRevenueClient;
}) {
  const { t } = useI18n();
  const client = useMemo<ShiftRevenueClient>(() => {
    if (injectedClient) return injectedClient;
    const clients = createAuthenticatedOperatorClients(backend!.config, backend!.session);
    return clients.shiftRevenue;
  }, [injectedClient, backend]);

  const [current, setCurrent] = useState<ShiftRevenueDto | null>(null);
  const [history, setHistory] = useState<ShiftRevenueDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    setLoading(true);
    Promise.all([client.current(branchId), client.history(branchId, 20)])
      .then(([cur, hist]) => {
        if (!active) return;
        setCurrent(cur);
        setHistory(hist.shifts.filter((s) => s.state === 'closed'));
      })
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, [client, branchId]);

  if (loading) {
    return <div className="op-shifts">{t('op.shifts.loading')}</div>;
  }

  return (
    <div className="op-shifts">
      <h2>{t('op.shifts.title')}</h2>

      {current ? (
        <section className="op-shifts-current">
          <h3>{t('op.shifts.current')}</h3>
          <div>{t('op.shifts.earned')}: {formatMoney(current.earned.total)}</div>
          <div>{t('op.shifts.time')}: {formatMoney(current.earned.time)}</div>
          <div>{t('op.shifts.goods')}: {formatMoney(current.earned.goods)}</div>
          <div>
            {t('op.shifts.inflow')}: {t('op.shifts.cash')} {formatMoney(current.inflow.cash)} · {t('op.shifts.nonCash')} {formatMoney(current.inflow.nonCash)}
          </div>
          <div>{t('op.shifts.walletTopUps')}: {formatMoney(current.inflow.walletTopUps)}</div>
          <div>
            {t('op.shifts.cashExpected')}: {formatMoney(current.cash.expected)}
            {current.cash.difference ? ` · ${t('op.shifts.cashDiff')}: ${formatMoney(current.cash.difference)}` : ''}
          </div>
        </section>
      ) : (
        <section className="op-shifts-empty">{t('op.shifts.noOpenShift')}</section>
      )}

      <section className="op-shifts-history">
        <h3>{t('op.shifts.history')}</h3>
        {history.length === 0 ? (
          <div>{t('op.shifts.historyEmpty')}</div>
        ) : (
          <ul>
            {history.map((s) => (
              <li key={s.shiftId}>
                {new Date(s.openedAtUtc).toLocaleDateString('ru-RU')} · {t('op.shifts.earned')} {formatMoney(s.earned.total)}
                {s.cash.difference ? ` · ${t('op.shifts.cashDiff')} ${formatMoney(s.cash.difference)}` : ''}
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
```

> The test seeds an **open** shift as `current`; history is filtered to closed shifts. Money `425000` minor → `4 250` (thin space). If `OperatorBackendContext` field names differ (`config`/`session`), match `NewsWorkspace.tsx`'s usage exactly.

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test ShiftsWorkspace`
Expected: PASS — 2 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/ShiftsWorkspace.tsx src/AFK4.Operator.App.Web/src/ShiftsWorkspace.test.tsx
git commit -m "feat(operator-web): add ShiftsWorkspace screen"
```

---

## Task 6: Nav + permission + i18n wiring

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorPermissions.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorData.ts`
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Modify: `packages/i18n/locales/ru.json`, `en.json`, `tg.json`

- [ ] **Step 1: Register the workspace id + permission rule**

In `operatorPermissions.ts`:
- Add `'shifts'` to the `workspaceIds` array.
- In `workspacePermissionRules`, add: `shifts: [permissionNames.viewReports],`
- If `permissionNames.viewReports` does not exist, add `viewReports: 'reports.view'` to `permissionNames` (check first — the reports feature implies it likely exists).

- [ ] **Step 2: Add the nav item**

In `operatorData.ts`:
- Import an icon: add `Wallet` to the existing `lucide-react` import.
- Add to `navItems` (place near the dashboard/reports entries): `{ labelKey: 'op.shifts.nav', icon: Wallet },`

> If `navItems` entries carry a workspace id field (check the `NavItem` type), set it to `'shifts'` to match the routing in App.tsx.

- [ ] **Step 3: Render the workspace in App.tsx**

In `App.tsx`, import the component: `import { ShiftsWorkspace } from './ShiftsWorkspace';`
Find how the active branch id is obtained for other workspaces (e.g. `backendContext`'s branch). Add, next to the `news` render block:

```typescript
{workspace === 'shifts' && backendContext !== null && (
  <ShiftsWorkspace backend={backendContext} branchId={backendContext.branchId} />
)}
```

> Match the exact branch-id accessor used by sibling workspaces (grep for `branchId` usage in App.tsx / backendContext). If the active branch is selected elsewhere, pass that value.

- [ ] **Step 4: Add i18n keys**

Add these keys to `packages/i18n/locales/ru.json` (and the matching translations to `en.json`, `tg.json`):

```json
"op.shifts.nav": "Смены",
"op.shifts.title": "Смены",
"op.shifts.loading": "Загрузка…",
"op.shifts.current": "Текущая смена",
"op.shifts.earned": "Заработано",
"op.shifts.time": "Время сессий",
"op.shifts.goods": "Товары",
"op.shifts.inflow": "Приток",
"op.shifts.cash": "нал",
"op.shifts.nonCash": "безнал",
"op.shifts.walletTopUps": "Пополнения кошелька",
"op.shifts.cashExpected": "Касса ожид.",
"op.shifts.cashDiff": "Расхождение",
"op.shifts.noOpenShift": "Нет открытой смены",
"op.shifts.history": "История",
"op.shifts.historyEmpty": "Закрытых смен пока нет"
```

English (`en.json`): "Shifts", "Shifts", "Loading…", "Current shift", "Earned", "Session time", "Goods", "Inflow", "cash", "card", "Wallet top-ups", "Cash expected", "Discrepancy", "No open shift", "History", "No closed shifts yet".

Tajik (`tg.json`): reuse Russian values as a placeholder if no Tajik translation is available yet (matches the current repo convention for tg fallbacks — verify against existing tg.json entries).

- [ ] **Step 5: Regenerate i18n + typecheck + test**

Run: `cd packages/i18n && /home/fedya/.bun/bin/bun run gen`
Then: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test && /home/fedya/.bun/bin/bun run build`
Expected: i18n regen succeeds; all bun tests pass; tsc + vite build clean.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorPermissions.ts src/AFK4.Operator.App.Web/src/operatorData.ts src/AFK4.Operator.App.Web/src/App.tsx packages/i18n/locales/ packages/i18n/src/
git commit -m "feat(operator-web): wire Shifts workspace into nav, permissions, i18n"
```

---

## Final verification

- [ ] **Backend:** `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --nologo` → all green.
- [ ] **Frontend:** `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test && /home/fedya/.bun/bin/bun run build` → all green.
- [ ] Manual smoke (optional): run the API + operator web, open the Shifts screen with an open shift, confirm earned/inflow/cash render.

## Notes / deferred (from spec, do NOT implement here)

- Calendar-day trends by branch `PreferredTimeZone` (separate follow-up).
- Packages (`PackagePurchase`) earned line, bonuses line, CSV export, charts, multi-branch roll-up.
- `DebtPayment` is intentionally not surfaced in inflow for MVP.
