# AFK4 Phase 5 Billing, Ledger, Tariffs, And Packages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the backend-authoritative Phase 5 billing foundation: immutable ledger, derived wallet and debt balances, prepaid and postpaid session flows, refunds, manual corrections, package purchase and consumption, tariff versioning, calculation, and money-command idempotency.

**Architecture:** Keep AFK4 as the existing ASP.NET Core modular monolith with PostgreSQL as source of truth. Add Billing as an explicit backend module that owns ledger entries, player wallet/debt projections, tariff/package rules, and money idempotency; it integrates with the existing Sessions module through application services, not direct cross-module table mutation. Ledger entries are append-only and balances are always derived from ledger rows, never stored as mutable balance fields.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core/Npgsql, EF Core InMemory tests, xUnit, shared DTO contracts in `AFK4.Shared.Contracts`.

---

## Scope

Phase 5 from the PRD covers:

- immutable ledger entries;
- wallet balances derived from ledger entries;
- prepaid wallet, postpaid debt, and package-backed gameplay flows;
- registered player billing foundation;
- top-ups, debt payments, refunds, reversals, and manual corrections;
- tariff versioning and calculation foundation;
- package definitions, purchases, bonus time grants, and package consumption;
- idempotency for every money-changing command.

This plan intentionally does not add:

- POS catalog, inventory, sales, returns, receipts, payment providers, shifts, or cash reconciliation;
- Operator App production UX, player search screens, cashier modals, hotkeys, or role-aware navigation;
- Agent enforcement changes or Player Shell session UI;
- web admin, local club server, microservices, or non-Windows agents;
- mutable wallet, debt, package, or bonus balance columns.

## Current Baseline

Already available and reused:

- staff sign-in, refresh-token rotation, and protected bearer token handling;
- branch-scoped staff authorization through `StaffAuthorizationService`;
- immutable audit writer and established allowed/denied audit pattern;
- persisted organizations, branches, zones, seats, device assignments, devices, device commands, and sessions;
- session lifecycle endpoints for start, extend, transfer, and end;
- session command idempotency pattern and SHA-256 key hashing;
- EF Core/Npgsql migrations and EF InMemory test host override;
- shared session contracts with `TariffRuleVersionId` preserved on sessions.

## File Structure

Create and modify these files:

```text
D:\afk4.net\
  docs\operations\local-postgres-smoke.md
  docs\progress\2026-05-12-vertical-slice-progress.md
  docs\superpowers\plans\2026-05-13-afk4-phase5-billing-ledger-tariffs-packages.md
  README.md
  src\AFK4.Shared.Contracts\
    Billing\BillingModeNames.cs
    Billing\CreatePlayerAccountRequest.cs
    Billing\LedgerAccountTypeNames.cs
    Billing\LedgerEntryDto.cs
    Billing\LedgerEntryTypeNames.cs
    Billing\ManualLedgerCorrectionRequest.cs
    Billing\MoneyDto.cs
    Billing\PayDebtRequest.cs
    Billing\PlayerAccountDto.cs
    Billing\PurchasePackageRequest.cs
    Billing\RefundLedgerEntryRequest.cs
    Billing\TopUpWalletRequest.cs
    Billing\WalletSummaryDto.cs
    Identity\StaffPermissionNames.cs
    Packages\CreatePackageDefinitionRequest.cs
    Packages\PackageDefinitionDto.cs
    Packages\PlayerPackageDto.cs
    Sessions\ExtendSessionRequest.cs
    Sessions\StartGuestSessionRequest.cs
    Tariffs\CalculateTariffRequest.cs
    Tariffs\CreateTariffRequest.cs
    Tariffs\CreateTariffVersionRequest.cs
    Tariffs\TariffCalculationResult.cs
    Tariffs\TariffDto.cs
    Tariffs\TariffVersionDto.cs
  src\AFK4.Platform.Api\
    Audit\AuditActionNames.cs
    Billing\BillingCommandIdempotencyKeyHasher.cs
    Billing\BillingCommandServiceResult.cs
    Billing\BillingEntryFactory.cs
    Billing\EfBillingCommandService.cs
    Billing\EfPackageService.cs
    Billing\EfTariffService.cs
    Billing\IBillingCommandService.cs
    Billing\IPackageService.cs
    Billing\ISessionBillingService.cs
    Billing\ITariffService.cs
    Billing\LedgerBalanceProjector.cs
    Billing\SessionBillingService.cs
    Data\BillingCommandIdempotencyEntity.cs
    Data\LedgerEntryEntity.cs
    Data\PackageDefinitionEntity.cs
    Data\PlayerAccountEntity.cs
    Data\PlayerPackageEntity.cs
    Data\PlatformDbContext.cs
    Data\TariffEntity.cs
    Data\TariffVersionEntity.cs
    Data\Migrations\<timestamp>_AddBillingLedgerTariffsPackages.cs
    Identity\PermissionCatalog.cs
    Program.cs
    Sessions\EfSessionCommandService.cs
    Sessions\ISessionCommandService.cs
  tests\AFK4.Shared.Contracts.Tests\
    BillingContractSerializationTests.cs
    PackageContractSerializationTests.cs
    TariffContractSerializationTests.cs
  tests\AFK4.Platform.Api.Tests\
    BillingEndpointTests.cs
    EfBillingCommandServiceTests.cs
    EfPackageServiceTests.cs
    EfSessionBillingIntegrationTests.cs
    EfTariffServiceTests.cs
    LedgerBalanceProjectorTests.cs
```

Responsibilities:

- `AFK4.Shared.Contracts.Billing`: transport DTOs for player accounts, ledger entries, wallet summaries, money commands, and billing modes.
- `AFK4.Shared.Contracts.Tariffs`: tariff create/version/calculate DTOs consumed by backend and future Operator workflows.
- `AFK4.Shared.Contracts.Packages`: package definition, purchase, and player package DTOs.
- `AFK4.Platform.Api.Billing`: application services for ledger writes, derived balances, tariff/package operations, session billing, and idempotency.
- `AFK4.Platform.Api.Data`: EF-owned billing, player account, tariff, package, and idempotency tables.
- `AFK4.Platform.Api.Sessions`: session command orchestration calls Billing through `ISessionBillingService`; Billing does not own session state transitions.

## Billing Rules

Ledger rules:

```text
Ledger entries are append-only.
No update path may change amount, quantity, type, account, player, session, package, reason, or reversal fields after insert.
Corrections append manual_correction entries.
Refunds append refund entries.
Reversals append reversal entries with ReversesLedgerEntryId.
Wallet balance is SUM(AmountMinorUnits) for AccountType == wallet.
Debt balance is SUM(AmountMinorUnits) for AccountType == debt.
Package remaining seconds are SUM(QuantitySeconds) for AccountType == package_time grouped by PlayerPackageId.
Bonus remaining seconds are SUM(QuantitySeconds) for AccountType == bonus_time grouped by PlayerPackageId.
```

Entry type names:

```text
top_up
gameplay_charge
package_purchase
package_consumption
bonus_grant
bonus_consumption
refund
manual_correction
postpaid_debt
debt_payment
reversal
```

Account type names:

```text
wallet
debt
package_time
bonus_time
```

Billing modes for session start and extension:

```text
prepaid_wallet
postpaid_debt
package
```

Session billing rules:

- prepaid wallet session start and extension calculate tariff charge, require available wallet balance, then append `gameplay_charge` to `wallet`;
- postpaid session start and extension calculate tariff charge and append `postpaid_debt` to `debt`;
- package session start and extension require a player package with enough derived remaining package or bonus seconds, then append `package_consumption` or `bonus_consumption`;
- rejected billing validation must prevent the session state change and device command dispatch;
- repeated command with the same idempotency key and same request returns the same response;
- repeated command with the same idempotency key and different request returns `409 Conflict`;
- money commands must write audit records for allowed and denied privileged attempts.

## Task 1: Shared Billing, Tariff, Package, And Permission Contracts

**Files:**

- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\BillingModeNames.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\CreatePlayerAccountRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\LedgerAccountTypeNames.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\LedgerEntryDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\LedgerEntryTypeNames.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\ManualLedgerCorrectionRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\MoneyDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\PayDebtRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\PlayerAccountDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\PurchasePackageRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\RefundLedgerEntryRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\TopUpWalletRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Billing\WalletSummaryDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Tariffs\CalculateTariffRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Tariffs\CreateTariffRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Tariffs\CreateTariffVersionRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Tariffs\TariffCalculationResult.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Tariffs\TariffDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Tariffs\TariffVersionDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Packages\CreatePackageDefinitionRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Packages\PackageDefinitionDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Packages\PlayerPackageDto.cs`
- Modify: `D:\afk4.net\src\AFK4.Shared.Contracts\Identity\StaffPermissionNames.cs`
- Modify: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\StartGuestSessionRequest.cs`
- Modify: `D:\afk4.net\src\AFK4.Shared.Contracts\Sessions\ExtendSessionRequest.cs`
- Create: `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\BillingContractSerializationTests.cs`
- Create: `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\TariffContractSerializationTests.cs`
- Create: `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\PackageContractSerializationTests.cs`

- [ ] **Step 1: Write failing billing contract serialization tests**

Create `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\BillingContractSerializationTests.cs`:

```csharp
using System.Text.Json;
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Tests;

public sealed class BillingContractSerializationTests
{
    [Fact]
    public void WalletSummary_RoundTripsDerivedBalancesAndRecentEntries()
    {
        var entry = new LedgerEntryDto(
            LedgerEntryId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            PlayerAccountId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            SessionId: null,
            PlayerPackageId: null,
            EntryType: LedgerEntryTypeNames.TopUp,
            AccountType: LedgerAccountTypeNames.Wallet,
            Amount: new MoneyDto("TJS", 5000),
            QuantitySeconds: 0,
            Description: "Cash top-up",
            Reason: "front-desk top-up",
            ReversesLedgerEntryId: null,
            CreatedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"));
        var summary = new WalletSummaryDto(
            PlayerAccountId: entry.PlayerAccountId,
            WalletBalance: new MoneyDto("TJS", 5000),
            DebtBalance: new MoneyDto("TJS", 0),
            RecentEntries: [entry]);

        var json = JsonSerializer.Serialize(summary);
        var copy = JsonSerializer.Deserialize<WalletSummaryDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(5000, copy.WalletBalance.MinorUnits);
        Assert.Equal(0, copy.DebtBalance.MinorUnits);
        Assert.Single(copy.RecentEntries);
        Assert.Equal(LedgerEntryTypeNames.TopUp, copy.RecentEntries[0].EntryType);
    }

    [Fact]
    public void SessionRequests_CanCarryBillingModeAndPlayerAccount()
    {
        var playerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
        var tariffVersionId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");
        var start = new AFK4.Shared.Contracts.Sessions.StartGuestSessionRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DurationMinutes: 60,
            TariffRuleVersionId: tariffVersionId.ToString("D"),
            IdempotencyKey: "start-prepaid-001",
            PlayerAccountId: playerAccountId,
            BillingMode: BillingModeNames.PrepaidWallet,
            TariffVersionId: tariffVersionId,
            PlayerPackageId: null);

        Assert.Equal(playerAccountId, start.PlayerAccountId);
        Assert.Equal(BillingModeNames.PrepaidWallet, start.BillingMode);
        Assert.Equal(tariffVersionId, start.TariffVersionId);
    }
}
```

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter BillingContractSerializationTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because the `Billing` namespace and session billing fields do not exist.

- [ ] **Step 2: Write failing tariff and package contract tests**

Create `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\TariffContractSerializationTests.cs`:

```csharp
using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Shared.Contracts.Tests;

public sealed class TariffContractSerializationTests
{
    [Fact]
    public void TariffCalculationResult_RoundTripsVersionAndAmount()
    {
        var result = new TariffCalculationResult(
            TariffId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            TariffVersionId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            TariffRuleVersionId: "bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb",
            DurationMinutes: 75,
            BillableMinutes: 90,
            Amount: new MoneyDto("TJS", 4500));

        var json = JsonSerializer.Serialize(result);
        var copy = JsonSerializer.Deserialize<TariffCalculationResult>(json);

        Assert.NotNull(copy);
        Assert.Equal(90, copy.BillableMinutes);
        Assert.Equal(4500, copy.Amount.MinorUnits);
        Assert.Equal(result.TariffRuleVersionId, copy.TariffRuleVersionId);
    }
}
```

Create `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\PackageContractSerializationTests.cs`:

```csharp
using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Packages;

namespace AFK4.Shared.Contracts.Tests;

public sealed class PackageContractSerializationTests
{
    [Fact]
    public void PlayerPackage_RoundTripsPurchasedSnapshotAndRemainingSeconds()
    {
        var package = new PlayerPackageDto(
            PlayerPackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            PackageDefinitionId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            PlayerAccountId: Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc"),
            Name: "Night 5h",
            PurchasedPrice: new MoneyDto("TJS", 4000),
            IncludedSeconds: 18000,
            BonusSeconds: 1800,
            RemainingIncludedSeconds: 12000,
            RemainingBonusSeconds: 1800,
            PurchasedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            ExpiresAtUtc: DateTimeOffset.Parse("2026-06-13T10:00:00Z"));

        var json = JsonSerializer.Serialize(package);
        var copy = JsonSerializer.Deserialize<PlayerPackageDto>(json);

        Assert.NotNull(copy);
        Assert.Equal("Night 5h", copy.Name);
        Assert.Equal(12000, copy.RemainingIncludedSeconds);
        Assert.Equal(1800, copy.RemainingBonusSeconds);
    }
}
```

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "TariffContractSerializationTests|PackageContractSerializationTests" --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because the `Tariffs` and `Packages` namespaces do not exist.

- [ ] **Step 3: Implement billing contract constants and DTOs**

Create these shared contracts:

```csharp
namespace AFK4.Shared.Contracts.Billing;

public static class BillingModeNames
{
    public const string PrepaidWallet = "prepaid_wallet";
    public const string PostpaidDebt = "postpaid_debt";
    public const string Package = "package";
}
```

```csharp
namespace AFK4.Shared.Contracts.Billing;

public static class LedgerEntryTypeNames
{
    public const string TopUp = "top_up";
    public const string GameplayCharge = "gameplay_charge";
    public const string PackagePurchase = "package_purchase";
    public const string PackageConsumption = "package_consumption";
    public const string BonusGrant = "bonus_grant";
    public const string BonusConsumption = "bonus_consumption";
    public const string Refund = "refund";
    public const string ManualCorrection = "manual_correction";
    public const string PostpaidDebt = "postpaid_debt";
    public const string DebtPayment = "debt_payment";
    public const string Reversal = "reversal";
}
```

```csharp
namespace AFK4.Shared.Contracts.Billing;

public static class LedgerAccountTypeNames
{
    public const string Wallet = "wallet";
    public const string Debt = "debt";
    public const string PackageTime = "package_time";
    public const string BonusTime = "bonus_time";
}
```

```csharp
namespace AFK4.Shared.Contracts.Billing;

public sealed record MoneyDto(string CurrencyCode, long MinorUnits);

public sealed record PlayerAccountDto(
    Guid PlayerAccountId,
    Guid OrganizationId,
    Guid HomeBranchId,
    string DisplayName,
    string? PhoneNumber,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed record CreatePlayerAccountRequest(
    Guid OrganizationId,
    string DisplayName,
    string? PhoneNumber,
    string IdempotencyKey);
```

```csharp
namespace AFK4.Shared.Contracts.Billing;

public sealed record LedgerEntryDto(
    Guid LedgerEntryId,
    Guid OrganizationId,
    Guid BranchId,
    Guid PlayerAccountId,
    Guid? SessionId,
    Guid? PlayerPackageId,
    string EntryType,
    string AccountType,
    MoneyDto Amount,
    int QuantitySeconds,
    string Description,
    string Reason,
    Guid? ReversesLedgerEntryId,
    Guid CreatedByStaffUserId,
    DateTimeOffset CreatedAtUtc);

public sealed record WalletSummaryDto(
    Guid PlayerAccountId,
    MoneyDto WalletBalance,
    MoneyDto DebtBalance,
    IReadOnlyList<LedgerEntryDto> RecentEntries);
```

```csharp
namespace AFK4.Shared.Contracts.Billing;

public sealed record TopUpWalletRequest(
    Guid OrganizationId,
    MoneyDto Amount,
    string Reason,
    string IdempotencyKey);

public sealed record ManualLedgerCorrectionRequest(
    Guid OrganizationId,
    string AccountType,
    MoneyDto Amount,
    int QuantitySeconds,
    string Reason,
    string IdempotencyKey);

public sealed record RefundLedgerEntryRequest(
    Guid OrganizationId,
    Guid LedgerEntryId,
    MoneyDto Amount,
    string Reason,
    string IdempotencyKey);

public sealed record PayDebtRequest(
    Guid OrganizationId,
    MoneyDto Amount,
    string Reason,
    string IdempotencyKey);

public sealed record PurchasePackageRequest(
    Guid OrganizationId,
    Guid PackageDefinitionId,
    string IdempotencyKey);
```

- [ ] **Step 4: Implement tariff, package, permission, and session request contracts**

Create tariff contracts:

```csharp
namespace AFK4.Shared.Contracts.Tariffs;

public sealed record CreateTariffRequest(
    Guid OrganizationId,
    string Name,
    string IdempotencyKey);

public sealed record CreateTariffVersionRequest(
    Guid OrganizationId,
    Guid TariffId,
    string CurrencyCode,
    long PricePerMinuteMinorUnits,
    int MinimumBillableMinutes,
    int RoundingIncrementMinutes,
    DateTimeOffset EffectiveFromUtc,
    string IdempotencyKey);

public sealed record CalculateTariffRequest(
    Guid OrganizationId,
    Guid TariffVersionId,
    int DurationMinutes);

public sealed record TariffDto(
    Guid TariffId,
    Guid OrganizationId,
    Guid BranchId,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed record TariffVersionDto(
    Guid TariffVersionId,
    Guid TariffId,
    int VersionNumber,
    string CurrencyCode,
    long PricePerMinuteMinorUnits,
    int MinimumBillableMinutes,
    int RoundingIncrementMinutes,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? RetiredAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record TariffCalculationResult(
    Guid TariffId,
    Guid TariffVersionId,
    string TariffRuleVersionId,
    int DurationMinutes,
    int BillableMinutes,
    AFK4.Shared.Contracts.Billing.MoneyDto Amount);
```

Create package contracts:

```csharp
namespace AFK4.Shared.Contracts.Packages;

public sealed record CreatePackageDefinitionRequest(
    Guid OrganizationId,
    string Name,
    AFK4.Shared.Contracts.Billing.MoneyDto Price,
    int IncludedSeconds,
    int BonusSeconds,
    int ExpiresAfterDays,
    string IdempotencyKey);

public sealed record PackageDefinitionDto(
    Guid PackageDefinitionId,
    Guid OrganizationId,
    Guid BranchId,
    string Name,
    AFK4.Shared.Contracts.Billing.MoneyDto Price,
    int IncludedSeconds,
    int BonusSeconds,
    int ExpiresAfterDays,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed record PlayerPackageDto(
    Guid PlayerPackageId,
    Guid PackageDefinitionId,
    Guid PlayerAccountId,
    string Name,
    AFK4.Shared.Contracts.Billing.MoneyDto PurchasedPrice,
    int IncludedSeconds,
    int BonusSeconds,
    int RemainingIncludedSeconds,
    int RemainingBonusSeconds,
    DateTimeOffset PurchasedAtUtc,
    DateTimeOffset? ExpiresAtUtc);
```

Append these permission names:

```csharp
public const string CreatePlayerAccount = "players.create";
public const string ViewBilling = "billing.view";
public const string TopUpWallet = "billing.wallet.top_up";
public const string RefundLedgerEntry = "billing.refund";
public const string ManualLedgerCorrection = "billing.manual_correction";
public const string PayDebt = "billing.debt.pay";
public const string ManageTariffs = "tariffs.manage";
public const string ManagePackages = "packages.manage";
public const string PurchasePackage = "packages.purchase";
```

Extend session request records with defaulted billing fields so existing callers remain source-compatible until tests migrate:

```csharp
Guid? PlayerAccountId = null,
string BillingMode = "",
Guid? TariffVersionId = null,
Guid? PlayerPackageId = null
```

- [ ] **Step 5: Run shared contract tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "BillingContractSerializationTests|TariffContractSerializationTests|PackageContractSerializationTests|SessionContractSerializationTests" --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 2: Billing Persistence Model And Derived Balance Projection

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Data\PlatformDbContext.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\PlayerAccountEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\LedgerEntryEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\BillingCommandIdempotencyEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\TariffEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\TariffVersionEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\PackageDefinitionEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\PlayerPackageEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\LedgerBalanceProjector.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\LedgerBalanceProjectorTests.cs`

- [ ] **Step 1: Write failing derived balance projection tests**

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\LedgerBalanceProjectorTests.cs`:

```csharp
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class LedgerBalanceProjectorTests
{
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task GetWalletSummaryAsync_DerivesWalletDebtAndRecentEntriesFromLedgerRows()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, 0));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.GameplayCharge, LedgerAccountTypeNames.Wallet, -1200, 0));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 700, 0));
        await db.SaveChangesAsync();

        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerAccountId, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal(3800, summary.WalletBalance.MinorUnits);
        Assert.Equal(700, summary.DebtBalance.MinorUnits);
        Assert.Equal(3, summary.RecentEntries.Count);
    }

    [Fact]
    public async Task GetPackageRemainingSecondsAsync_DerivesRemainingPackageAndBonusSeconds()
    {
        await using var db = CreateDbContext();
        var packageId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");
        SeedPlayer(db);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.PackagePurchase, LedgerAccountTypeNames.PackageTime, 0, 3600, packageId));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.BonusGrant, LedgerAccountTypeNames.BonusTime, 0, 600, packageId));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.PackageConsumption, LedgerAccountTypeNames.PackageTime, 0, -900, packageId));
        await db.SaveChangesAsync();

        var remaining = await LedgerBalanceProjector.GetPackageRemainingSecondsAsync(db, packageId, CancellationToken.None);

        Assert.Equal(2700, remaining.IncludedSeconds);
        Assert.Equal(600, remaining.BonusSeconds);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static void SeedPlayer(PlatformDbContext db)
    {
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player One",
            PhoneNumber = null,
            IsActive = true,
            CreatedAtUtc = Now
        });
    }

    private static LedgerEntryEntity CreateEntry(
        string entryType,
        string accountType,
        long amountMinorUnits,
        int quantitySeconds,
        Guid? packageId = null)
    {
        return new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = PlayerAccountId,
            SessionId = null,
            PlayerPackageId = packageId,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = quantitySeconds,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "test",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = StaffUserId,
            CreatedAtUtc = Now
        };
    }
}
```

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter LedgerBalanceProjectorTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because billing entities and `LedgerBalanceProjector` do not exist.

- [ ] **Step 2: Implement EF entities without mutable balance fields**

Create `PlayerAccountEntity`:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class PlayerAccountEntity
{
    public Guid PlayerAccountId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid HomeBranchId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

Create `LedgerEntryEntity`:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class LedgerEntryEntity
{
    public Guid LedgerEntryId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid PlayerAccountId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? PlayerPackageId { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public long AmountMinorUnits { get; set; }
    public int QuantitySeconds { get; set; }
    public string CurrencyCode { get; set; } = "TJS";
    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Guid? ReversesLedgerEntryId { get; set; }
    public Guid CreatedByStaffUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

Create `BillingCommandIdempotencyEntity`:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class BillingCommandIdempotencyEntity
{
    public Guid BillingCommandIdempotencyId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string IdempotencyKeyHash { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
```

Create tariff and package entities:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class TariffEntity
{
    public Guid TariffId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class TariffVersionEntity
{
    public Guid TariffVersionId { get; set; }
    public Guid TariffId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public int VersionNumber { get; set; }
    public string CurrencyCode { get; set; } = "TJS";
    public long PricePerMinuteMinorUnits { get; set; }
    public int MinimumBillableMinutes { get; set; }
    public int RoundingIncrementMinutes { get; set; }
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? RetiredAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class PackageDefinitionEntity
{
    public Guid PackageDefinitionId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "TJS";
    public long PriceMinorUnits { get; set; }
    public int IncludedSeconds { get; set; }
    public int BonusSeconds { get; set; }
    public int ExpiresAfterDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class PlayerPackageEntity
{
    public Guid PlayerPackageId { get; set; }
    public Guid PackageDefinitionId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid PlayerAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "TJS";
    public long PurchasedPriceMinorUnits { get; set; }
    public int IncludedSeconds { get; set; }
    public int BonusSeconds { get; set; }
    public DateTimeOffset PurchasedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}
```

- [ ] **Step 3: Configure `PlatformDbContext`**

Add DbSets:

```csharp
public DbSet<PlayerAccountEntity> PlayerAccounts => Set<PlayerAccountEntity>();
public DbSet<LedgerEntryEntity> LedgerEntries => Set<LedgerEntryEntity>();
public DbSet<BillingCommandIdempotencyEntity> BillingCommandIdempotency => Set<BillingCommandIdempotencyEntity>();
public DbSet<TariffEntity> Tariffs => Set<TariffEntity>();
public DbSet<TariffVersionEntity> TariffVersions => Set<TariffVersionEntity>();
public DbSet<PackageDefinitionEntity> PackageDefinitions => Set<PackageDefinitionEntity>();
public DbSet<PlayerPackageEntity> PlayerPackages => Set<PlayerPackageEntity>();
```

Add table configuration:

```csharp
modelBuilder.Entity<PlayerAccountEntity>(entity =>
{
    entity.ToTable("player_accounts");
    entity.HasKey(player => player.PlayerAccountId);
    entity.Property(player => player.DisplayName).HasMaxLength(160).IsRequired();
    entity.Property(player => player.PhoneNumber).HasMaxLength(64);
    entity.HasIndex(player => new { player.OrganizationId, player.HomeBranchId });
});

modelBuilder.Entity<LedgerEntryEntity>(entity =>
{
    entity.ToTable("ledger_entries");
    entity.HasKey(entry => entry.LedgerEntryId);
    entity.Property(entry => entry.EntryType).HasMaxLength(64).IsRequired();
    entity.Property(entry => entry.AccountType).HasMaxLength(64).IsRequired();
    entity.Property(entry => entry.CurrencyCode).HasMaxLength(3).IsRequired();
    entity.Property(entry => entry.Description).HasMaxLength(240).IsRequired();
    entity.Property(entry => entry.Reason).HasMaxLength(512).IsRequired();
    entity.HasIndex(entry => new { entry.OrganizationId, entry.BranchId, entry.CreatedAtUtc });
    entity.HasIndex(entry => new { entry.PlayerAccountId, entry.CreatedAtUtc });
    entity.HasIndex(entry => entry.SessionId);
    entity.HasIndex(entry => entry.PlayerPackageId);
    entity.HasIndex(entry => entry.ReversesLedgerEntryId);
});

modelBuilder.Entity<BillingCommandIdempotencyEntity>(entity =>
{
    entity.ToTable("billing_command_idempotency");
    entity.HasKey(record => record.BillingCommandIdempotencyId);
    entity.Property(record => record.Operation).HasMaxLength(64).IsRequired();
    entity.Property(record => record.IdempotencyKeyHash).HasMaxLength(128).IsRequired();
    entity.Property(record => record.RequestHash).HasMaxLength(128).IsRequired();
    entity.Property(record => record.ResponseJson).HasColumnType("jsonb").IsRequired();
    entity.HasIndex(record => new { record.OrganizationId, record.BranchId, record.Operation, record.IdempotencyKeyHash }).IsUnique();
});
```

Configure tariffs and packages:

```csharp
modelBuilder.Entity<TariffEntity>(entity =>
{
    entity.ToTable("tariffs");
    entity.HasKey(tariff => tariff.TariffId);
    entity.Property(tariff => tariff.Name).HasMaxLength(160).IsRequired();
    entity.HasIndex(tariff => new { tariff.OrganizationId, tariff.BranchId, tariff.Name }).IsUnique();
});

modelBuilder.Entity<TariffVersionEntity>(entity =>
{
    entity.ToTable("tariff_versions");
    entity.HasKey(version => version.TariffVersionId);
    entity.Property(version => version.CurrencyCode).HasMaxLength(3).IsRequired();
    entity.HasIndex(version => new { version.TariffId, version.VersionNumber }).IsUnique();
    entity.HasIndex(version => new { version.OrganizationId, version.BranchId, version.EffectiveFromUtc });
});

modelBuilder.Entity<PackageDefinitionEntity>(entity =>
{
    entity.ToTable("package_definitions");
    entity.HasKey(package => package.PackageDefinitionId);
    entity.Property(package => package.Name).HasMaxLength(160).IsRequired();
    entity.Property(package => package.CurrencyCode).HasMaxLength(3).IsRequired();
    entity.HasIndex(package => new { package.OrganizationId, package.BranchId, package.Name }).IsUnique();
});

modelBuilder.Entity<PlayerPackageEntity>(entity =>
{
    entity.ToTable("player_packages");
    entity.HasKey(package => package.PlayerPackageId);
    entity.Property(package => package.Name).HasMaxLength(160).IsRequired();
    entity.Property(package => package.CurrencyCode).HasMaxLength(3).IsRequired();
    entity.HasIndex(package => new { package.PlayerAccountId, package.PurchasedAtUtc });
    entity.HasIndex(package => new { package.OrganizationId, package.BranchId });
});
```

- [ ] **Step 4: Implement derived balance projector**

Create `LedgerBalanceProjector`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Billing;

public sealed record PackageRemainingSeconds(int IncludedSeconds, int BonusSeconds);

public static class LedgerBalanceProjector
{
    public static async Task<WalletSummaryDto?> GetWalletSummaryAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        var player = await dbContext.PlayerAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PlayerAccountId == playerAccountId, cancellationToken);

        if (player is null)
        {
            return null;
        }

        var entries = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerAccountId == playerAccountId)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ThenByDescending(entry => entry.LedgerEntryId)
            .Take(25)
            .ToListAsync(cancellationToken);

        var wallet = await SumAmountAsync(dbContext, playerAccountId, LedgerAccountTypeNames.Wallet, cancellationToken);
        var debt = await SumAmountAsync(dbContext, playerAccountId, LedgerAccountTypeNames.Debt, cancellationToken);

        return new WalletSummaryDto(
            playerAccountId,
            new MoneyDto("TJS", wallet),
            new MoneyDto("TJS", debt),
            entries.Select(ToDto).ToList());
    }

    public static async Task<PackageRemainingSeconds> GetPackageRemainingSecondsAsync(
        PlatformDbContext dbContext,
        Guid playerPackageId,
        CancellationToken cancellationToken)
    {
        var included = await SumQuantityAsync(dbContext, playerPackageId, LedgerAccountTypeNames.PackageTime, cancellationToken);
        var bonus = await SumQuantityAsync(dbContext, playerPackageId, LedgerAccountTypeNames.BonusTime, cancellationToken);

        return new PackageRemainingSeconds(included, bonus);
    }

    private static async Task<long> SumAmountAsync(PlatformDbContext dbContext, Guid playerAccountId, string accountType, CancellationToken cancellationToken)
    {
        return await dbContext.LedgerEntries
            .Where(entry => entry.PlayerAccountId == playerAccountId && entry.AccountType == accountType)
            .SumAsync(entry => (long?)entry.AmountMinorUnits, cancellationToken) ?? 0;
    }

    private static async Task<int> SumQuantityAsync(PlatformDbContext dbContext, Guid playerPackageId, string accountType, CancellationToken cancellationToken)
    {
        return await dbContext.LedgerEntries
            .Where(entry => entry.PlayerPackageId == playerPackageId && entry.AccountType == accountType)
            .SumAsync(entry => (int?)entry.QuantitySeconds, cancellationToken) ?? 0;
    }

    public static LedgerEntryDto ToDto(LedgerEntryEntity entry)
    {
        return new LedgerEntryDto(
            entry.LedgerEntryId,
            entry.OrganizationId,
            entry.BranchId,
            entry.PlayerAccountId,
            entry.SessionId,
            entry.PlayerPackageId,
            entry.EntryType,
            entry.AccountType,
            new MoneyDto(entry.CurrencyCode, entry.AmountMinorUnits),
            entry.QuantitySeconds,
            entry.Description,
            entry.Reason,
            entry.ReversesLedgerEntryId,
            entry.CreatedByStaffUserId,
            entry.CreatedAtUtc);
    }
}
```

- [ ] **Step 5: Run projection tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter LedgerBalanceProjectorTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 3: Ledger Command Service, Idempotency, Refunds, And Corrections

**Files:**

- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\BillingCommandIdempotencyKeyHasher.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\BillingCommandServiceResult.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\BillingEntryFactory.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\EfBillingCommandService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\IBillingCommandService.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfBillingCommandServiceTests.cs`

- [ ] **Step 1: Write failing ledger command service tests**

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfBillingCommandServiceTests.cs` with tests for:

```text
CreatePlayerAccountAsync creates a minimal active player account and is idempotent.
TopUpWalletAsync appends a top_up wallet entry and returns derived wallet balance.
RefundLedgerEntryAsync appends a refund entry and does not mutate the original entry.
ManualCorrectionAsync appends manual_correction and requires non-empty reason.
PayDebtAsync appends debt_payment with negative debt amount and rejects overpayment.
Reusing an idempotency key with a different request returns Conflict.
```

The top-up test must assert:

```csharp
Assert.Equal(LedgerEntryTypeNames.TopUp, entry.EntryType);
Assert.Equal(LedgerAccountTypeNames.Wallet, entry.AccountType);
Assert.Equal(5000, entry.AmountMinorUnits);
Assert.Equal(5000, summary.WalletBalance.MinorUnits);
Assert.Single(db.BillingCommandIdempotency);
```

The refund test must assert:

```csharp
Assert.Equal(LedgerEntryTypeNames.Refund, refund.EntryType);
Assert.Equal(original.LedgerEntryId, refund.ReversesLedgerEntryId);
Assert.Equal(-original.AmountMinorUnits, refund.AmountMinorUnits);
Assert.Equal(original.AmountMinorUnits, originalAfterRefund.AmountMinorUnits);
```

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfBillingCommandServiceTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because the command service does not exist.

- [ ] **Step 2: Implement idempotency hasher and result type**

Create:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace AFK4.Platform.Api.Billing;

public static class BillingCommandIdempotencyKeyHasher
{
    public static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()))).ToLowerInvariant();
    }
}
```

```csharp
namespace AFK4.Platform.Api.Billing;

public sealed record BillingCommandServiceResult<TResponse>(
    bool Succeeded,
    bool Conflict,
    bool NotFound,
    string? Error,
    TResponse? Response)
{
    public static BillingCommandServiceResult<TResponse> Ok(TResponse response) => new(true, false, false, null, response);
    public static BillingCommandServiceResult<TResponse> RequestConflict(string error) => new(false, true, false, error, default);
    public static BillingCommandServiceResult<TResponse> Missing(string error) => new(false, false, true, error, default);
    public static BillingCommandServiceResult<TResponse> Invalid(string error) => new(false, false, false, error, default);
}
```

- [ ] **Step 3: Implement command service interface**

Create:

```csharp
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.Billing;

public interface IBillingCommandService
{
    Task<BillingCommandServiceResult<PlayerAccountDto>> CreatePlayerAccountAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreatePlayerAccountRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<WalletSummaryDto>> TopUpWalletAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        TopUpWalletRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<LedgerEntryDto>> RefundLedgerEntryAsync(
        Guid branchId,
        Guid actorStaffUserId,
        RefundLedgerEntryRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<WalletSummaryDto>> ManualCorrectionAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        ManualLedgerCorrectionRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<WalletSummaryDto>> PayDebtAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        PayDebtRequest request,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement `EfBillingCommandService`**

Implementation rules:

```text
Use `ExecuteInTransactionAsync` with relational transaction support, matching `EfSessionCommandService`.
Reject empty idempotency keys before any ledger write.
Hash request JSON with `JsonSerializerOptions(JsonSerializerDefaults.Web)`.
Store idempotent responses in `billing_command_idempotency`.
Top-up amount must be positive.
Refund amount must be positive and cannot exceed the absolute refundable amount of the original entry.
Refund appends a refund entry with `ReversesLedgerEntryId` set to the original entry.
Manual correction reason must be non-empty and at least 8 trimmed characters.
Debt payment amount must be positive and cannot exceed current derived debt balance.
Debt payment appends negative amount to the `debt` account.
No service method updates or deletes existing ledger entries.
```

Use `BillingEntryFactory` to centralize ledger row creation:

```csharp
using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Billing;

public static class BillingEntryFactory
{
    public static LedgerEntryEntity Create(
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        Guid? sessionId,
        Guid? playerPackageId,
        string entryType,
        string accountType,
        long amountMinorUnits,
        int quantitySeconds,
        string currencyCode,
        string description,
        string reason,
        Guid? reversesLedgerEntryId,
        Guid actorStaffUserId,
        DateTimeOffset createdAtUtc)
    {
        return new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            PlayerAccountId = playerAccountId,
            SessionId = sessionId,
            PlayerPackageId = playerPackageId,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = quantitySeconds,
            CurrencyCode = currencyCode,
            Description = description,
            Reason = reason,
            ReversesLedgerEntryId = reversesLedgerEntryId,
            CreatedByStaffUserId = actorStaffUserId,
            CreatedAtUtc = createdAtUtc
        };
    }
}
```

- [ ] **Step 5: Run ledger command service tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfBillingCommandServiceTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 4: Tariff Versioning And Calculation Foundation

**Files:**

- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\EfTariffService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\ITariffService.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfTariffServiceTests.cs`

- [ ] **Step 1: Write failing tariff service tests**

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfTariffServiceTests.cs` with tests for:

```text
CreateTariffAsync creates a branch tariff and stores idempotency.
CreateTariffVersionAsync assigns version number 1 for the first version.
CreateTariffVersionAsync retires the previous active version when a new version starts.
CalculateAsync preserves TariffVersionId and returns rounded billable minutes.
Reusing an idempotency key with a different tariff version request returns Conflict.
```

The calculation test must assert:

```csharp
Assert.Equal(75, result.DurationMinutes);
Assert.Equal(90, result.BillableMinutes);
Assert.Equal(4500, result.Amount.MinorUnits);
Assert.Equal(version.TariffVersionId.ToString("D"), result.TariffRuleVersionId);
```

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfTariffServiceTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because `ITariffService` and `EfTariffService` do not exist.

- [ ] **Step 2: Implement tariff service interface**

Create:

```csharp
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Platform.Api.Billing;

public interface ITariffService
{
    Task<BillingCommandServiceResult<TariffDto>> CreateTariffAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateTariffRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<TariffVersionDto>> CreateTariffVersionAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateTariffVersionRequest request,
        CancellationToken cancellationToken);

    Task<TariffCalculationResult?> CalculateAsync(
        Guid branchId,
        CalculateTariffRequest request,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Implement calculation rules**

`EfTariffService.CalculateAsync` must:

```text
Load the tariff version by `TariffVersionId`, `OrganizationId`, and `BranchId`.
Reject inactive or retired versions by returning null.
Set billable minutes to max(DurationMinutes, MinimumBillableMinutes).
Round billable minutes up to the nearest RoundingIncrementMinutes when the increment is greater than 1.
Calculate amount as billable minutes multiplied by PricePerMinuteMinorUnits.
Return TariffRuleVersionId as TariffVersionId.ToString("D").
```

Use this helper inside the service:

```csharp
private static int RoundBillableMinutes(int durationMinutes, int minimumBillableMinutes, int roundingIncrementMinutes)
{
    var billable = Math.Max(durationMinutes, minimumBillableMinutes);
    if (roundingIncrementMinutes <= 1)
    {
        return billable;
    }

    var remainder = billable % roundingIncrementMinutes;
    return remainder == 0
        ? billable
        : billable + roundingIncrementMinutes - remainder;
}
```

- [ ] **Step 4: Run tariff service tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfTariffServiceTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 5: Package Definitions, Purchases, Bonus Grants, And Consumption

**Files:**

- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\EfPackageService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\IPackageService.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfPackageServiceTests.cs`

- [ ] **Step 1: Write failing package service tests**

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfPackageServiceTests.cs` with tests for:

```text
CreatePackageDefinitionAsync creates an active branch package.
PurchasePackageAsync debits wallet, creates PlayerPackageEntity, grants package_time, and grants bonus_time.
PurchasePackageAsync rejects insufficient wallet balance.
ConsumePackageTimeAsync consumes bonus seconds first, then included package seconds.
ConsumePackageTimeAsync rejects expired packages.
Repeated package purchase with the same idempotency key returns the same player package.
```

The purchase test must assert:

```csharp
Assert.Equal(3, db.LedgerEntries.Count());
Assert.Contains(db.LedgerEntries, entry => entry.EntryType == LedgerEntryTypeNames.PackagePurchase && entry.AccountType == LedgerAccountTypeNames.Wallet && entry.AmountMinorUnits == -4000);
Assert.Contains(db.LedgerEntries, entry => entry.EntryType == LedgerEntryTypeNames.PackagePurchase && entry.AccountType == LedgerAccountTypeNames.PackageTime && entry.QuantitySeconds == 18000);
Assert.Contains(db.LedgerEntries, entry => entry.EntryType == LedgerEntryTypeNames.BonusGrant && entry.AccountType == LedgerAccountTypeNames.BonusTime && entry.QuantitySeconds == 1800);
```

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfPackageServiceTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because package service types do not exist.

- [ ] **Step 2: Implement package service interface**

Create:

```csharp
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Packages;

namespace AFK4.Platform.Api.Billing;

public interface IPackageService
{
    Task<BillingCommandServiceResult<PackageDefinitionDto>> CreatePackageDefinitionAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreatePackageDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PlayerPackageDto>> PurchasePackageAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        PurchasePackageRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<IReadOnlyList<LedgerEntryDto>>> ConsumePackageTimeAsync(
        Guid playerAccountId,
        Guid playerPackageId,
        Guid branchId,
        Guid sessionId,
        Guid actorStaffUserId,
        int durationSeconds,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Implement package service rules**

Implementation rules:

```text
Package price must be positive.
IncludedSeconds must be positive.
BonusSeconds can be zero or positive.
ExpiresAfterDays must be positive.
Purchase requires wallet balance >= package price.
Purchase appends wallet package_purchase debit, package_time package_purchase grant, and bonus_time bonus_grant when BonusSeconds > 0.
PlayerPackageEntity stores the purchased package snapshot and expiry timestamp.
Consumption checks expiry before writing ledger rows.
Consumption consumes bonus_time first when available, then package_time.
Consumption creates one or two append-only ledger rows with negative QuantitySeconds.
Package remaining values are derived with `LedgerBalanceProjector`.
```

- [ ] **Step 4: Run package service tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfPackageServiceTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 6: Protected Billing, Tariff, And Package Endpoints

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Audit\AuditActionNames.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Identity\PermissionCatalog.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\BillingEndpointTests.cs`

- [ ] **Step 1: Write failing endpoint tests**

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\BillingEndpointTests.cs` with tests for:

```text
POST /api/branches/{branchId}/players without bearer returns 401.
POST /api/branches/{branchId}/players with cashier creates player and audit.
POST /api/players/{playerAccountId}/wallet/top-ups with cashier appends top-up.
POST /api/players/{playerAccountId}/ledger/manual-corrections with cashier returns 403 and writes denied audit.
POST /api/players/{playerAccountId}/ledger/manual-corrections with shift supervisor succeeds.
GET /api/players/{playerAccountId}/wallet-summary with accountant returns derived balances.
POST /api/branches/{branchId}/tariffs with branch manager creates tariff.
POST /api/branches/{branchId}/tariffs/{tariffId}/versions with branch manager creates tariff version.
POST /api/branches/{branchId}/tariffs/calculate returns tariff calculation for authorized staff.
POST /api/branches/{branchId}/packages with branch manager creates package definition.
POST /api/players/{playerAccountId}/packages/purchases with cashier purchases package.
Duplicate idempotency key with different endpoint request returns 409.
```

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter BillingEndpointTests --no-restore -p:UseSharedCompilation=false
```

Expected: route `404` or compile failure because Phase 5 endpoints are absent.

- [ ] **Step 2: Add audit action names**

Append:

```csharp
public const string CreatePlayerAccount = "players.create";
public const string TopUpWallet = "billing.wallet.top_up";
public const string RefundLedgerEntry = "billing.refund";
public const string ManualLedgerCorrection = "billing.manual_correction";
public const string PayDebt = "billing.debt.pay";
public const string CreateTariff = "tariffs.create";
public const string CreateTariffVersion = "tariffs.versions.create";
public const string CreatePackageDefinition = "packages.create";
public const string PurchasePackage = "packages.purchase";
```

- [ ] **Step 3: Update role-to-permission mapping**

Map permissions:

```text
owner: all Phase 5 permissions.
branch_manager: all Phase 5 permissions.
shift_supervisor: players.create, billing.view, billing.wallet.top_up, billing.refund, billing.manual_correction, billing.debt.pay, packages.purchase.
cashier_operator: players.create, billing.view, billing.wallet.top_up, billing.debt.pay, packages.purchase.
accountant_auditor: billing.view.
technician: no Phase 5 billing, tariff, or package permissions.
```

- [ ] **Step 4: Register services**

In `Program.cs` add:

```csharp
builder.Services.AddScoped<IBillingCommandService, EfBillingCommandService>();
builder.Services.AddScoped<ITariffService, EfTariffService>();
builder.Services.AddScoped<IPackageService, EfPackageService>();
builder.Services.AddScoped<ISessionBillingService, SessionBillingService>();
```

- [ ] **Step 5: Map endpoints**

Add these endpoints:

```text
POST /api/branches/{branchId:guid}/players
GET  /api/players/{playerAccountId:guid}/wallet-summary
POST /api/players/{playerAccountId:guid}/wallet/top-ups
POST /api/players/{playerAccountId:guid}/ledger/{ledgerEntryId:guid}/refunds
POST /api/players/{playerAccountId:guid}/ledger/manual-corrections
POST /api/players/{playerAccountId:guid}/debts/payments
POST /api/branches/{branchId:guid}/tariffs
POST /api/branches/{branchId:guid}/tariffs/{tariffId:guid}/versions
POST /api/branches/{branchId:guid}/tariffs/calculate
POST /api/branches/{branchId:guid}/packages
POST /api/players/{playerAccountId:guid}/packages/purchases
GET  /api/players/{playerAccountId:guid}/packages
```

Endpoint rules:

```text
Use StaffAuthorizationService.RequireBranchPermissionAsync for every route.
Return 401 for missing staff context.
Return 403 and write denied audit for authenticated staff without permission on money-changing routes.
Validate request.OrganizationId equals authenticated staff organization.
Load player account before player-scoped writes and require HomeBranchId authorization.
Return 404 for unknown player, tariff, package, or ledger entry after authorization succeeds.
Return 409 for idempotency conflicts.
Return 400 for validation failures from the service layer.
Write succeeded audit records for all money-changing routes.
Do not write audit for wallet-summary reads in this slice.
```

- [ ] **Step 6: Run endpoint tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter BillingEndpointTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 7: Session Billing Integration

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\ISessionCommandService.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Sessions\EfSessionCommandService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\ISessionBillingService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\SessionBillingService.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfSessionBillingIntegrationTests.cs`
- Modify: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfSessionCommandServiceTests.cs`
- Modify: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\SessionEndpointTests.cs`

- [ ] **Step 1: Write failing session billing integration tests**

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\EfSessionBillingIntegrationTests.cs` with tests for:

```text
StartGuestSessionAsync with prepaid_wallet debits wallet before unlock command.
StartGuestSessionAsync with prepaid_wallet rejects insufficient funds and dispatches no device command.
ExtendSessionAsync with prepaid_wallet debits additional gameplay charge and refreshes lease.
StartGuestSessionAsync with postpaid_debt appends postpaid_debt and allows session.
StartGuestSessionAsync with package appends package_consumption and allows session.
StartGuestSessionAsync with package rejects insufficient package seconds and dispatches no device command.
Session idempotency replay returns the original response without duplicate ledger entries.
```

The prepaid success test must assert:

```csharp
Assert.Contains(db.LedgerEntries, entry =>
    entry.EntryType == LedgerEntryTypeNames.GameplayCharge &&
    entry.AccountType == LedgerAccountTypeNames.Wallet &&
    entry.AmountMinorUnits < 0 &&
    entry.SessionId == result.Response!.Session.SessionId);
Assert.Single(dispatcher.Calls);
```

The insufficient funds test must assert:

```csharp
Assert.False(result.Succeeded);
Assert.Empty(db.Sessions);
Assert.Empty(db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.GameplayCharge));
Assert.Empty(dispatcher.Calls);
```

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfSessionBillingIntegrationTests --no-restore -p:UseSharedCompilation=false
```

Expected: compile failure because session billing service types do not exist.

- [ ] **Step 2: Implement session billing interface**

Create:

```csharp
namespace AFK4.Platform.Api.Billing;

public sealed record SessionBillingValidationResult(
    bool Succeeded,
    string? Error,
    string TariffRuleVersionId,
    Guid? TariffVersionId,
    int BillableSeconds,
    long AmountMinorUnits,
    string CurrencyCode);

public interface ISessionBillingService
{
    Task<SessionBillingValidationResult> ValidateStartAsync(
        Guid organizationId,
        Guid branchId,
        Guid? playerAccountId,
        string billingMode,
        Guid? tariffVersionId,
        Guid? playerPackageId,
        int durationMinutes,
        CancellationToken cancellationToken);

    Task<SessionBillingValidationResult> ValidateExtendAsync(
        Guid organizationId,
        Guid branchId,
        Guid? playerAccountId,
        string billingMode,
        Guid? tariffVersionId,
        Guid? playerPackageId,
        int additionalMinutes,
        CancellationToken cancellationToken);

    Task AppendStartLedgerEntriesAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionBillingValidationResult validation,
        Guid playerAccountId,
        Guid? playerPackageId,
        string billingMode,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task AppendExtendLedgerEntriesAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionBillingValidationResult validation,
        Guid playerAccountId,
        Guid? playerPackageId,
        string billingMode,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Implement billing validation rules**

`SessionBillingService` must:

```text
Reject empty BillingMode for Phase 5 session requests.
Reject null PlayerAccountId for all Phase 5 billing modes.
For prepaid_wallet, require TariffVersionId and wallet balance >= calculated amount.
For postpaid_debt, require TariffVersionId and append debt without wallet balance check.
For package, require PlayerPackageId and enough derived remaining seconds.
Use ITariffService.CalculateAsync for prepaid_wallet and postpaid_debt.
Set TariffRuleVersionId from tariff calculation result.
For package, keep TariffRuleVersionId as `package:{PlayerPackageId:D}`.
Return clear validation errors that endpoint tests can assert.
```

- [ ] **Step 4: Integrate with `EfSessionCommandService`**

Modify `EfSessionCommandService` constructor to accept `ISessionBillingService`.

For `StartGuestSessionAsync`:

```text
Validate billing after seat/device/session availability checks and before creating SessionEntity.
Use validation.TariffRuleVersionId as the persisted session TariffRuleVersionId.
Create session and lease.
Append billing ledger entries in the same transaction before device command dispatch.
Dispatch unlock only after session and ledger entries are saved successfully.
Store idempotent response after successful session, ledger, and command creation.
```

For `ExtendSessionAsync`:

```text
Validate billing before mutating EndsAtUtc.
Append ledger entries in the same transaction as EndsAtUtc and lease refresh.
Dispatch refresh-session-lease only after session and ledger entries are saved successfully.
```

Update existing tests to provide a fake `ISessionBillingService` that treats legacy Phase 4 requests as valid inside existing non-billing tests. Add integration tests that use the real service and explicit Phase 5 billing fields.

- [ ] **Step 5: Run session billing tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfSessionBillingIntegrationTests|EfSessionCommandServiceTests|SessionEndpointTests" --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

## Task 8: EF Migration, Smoke Runbook, README, And Progress

**Files:**

- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\Migrations\<timestamp>_AddBillingLedgerTariffsPackages.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Data\Migrations\PlatformDbContextModelSnapshot.cs`
- Modify: `D:\afk4.net\docs\operations\local-postgres-smoke.md`
- Modify: `D:\afk4.net\docs\progress\2026-05-12-vertical-slice-progress.md`
- Modify: `D:\afk4.net\README.md`

- [ ] **Step 1: Create EF migration**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations add AddBillingLedgerTariffsPackages --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

Expected:

```text
Done. To undo this action, use 'ef migrations remove'
```

- [ ] **Step 2: Review migration**

Verify the migration creates:

```text
player_accounts
ledger_entries
billing_command_idempotency
tariffs
tariff_versions
package_definitions
player_packages
```

Verify indexes exist for:

```text
player_accounts by organization and home branch
ledger_entries by organization, branch, and CreatedAtUtc
ledger_entries by PlayerAccountId and CreatedAtUtc
ledger_entries by SessionId
ledger_entries by PlayerPackageId
billing_command_idempotency unique by organization, branch, operation, idempotency key hash
tariffs unique by organization, branch, and name
tariff_versions unique by tariff and version number
package_definitions unique by organization, branch, and name
player_packages by player account and purchase timestamp
```

Verify the migration does not add:

```text
wallet_balance
balance
debt_balance
remaining_seconds
remaining_bonus_seconds
mutable package balance columns
```

- [ ] **Step 3: Update local PostgreSQL smoke runbook**

Add a Phase 5 smoke path:

```text
Sign in as cashier/operator for player creation, top-up, package purchase, and session start.
Create a player account.
Top up wallet with idempotency key smoke-topup-001.
Create tariff and tariff version as branch manager or owner.
Calculate tariff for 60 minutes.
Start prepaid wallet session using the tariff version and idempotency key smoke-start-prepaid-001.
Confirm repeated prepaid start returns the same session id and does not duplicate ledger entries.
Create package definition as branch manager or owner.
Purchase package with idempotency key smoke-package-buy-001.
Start or extend a package-backed session with idempotency key smoke-start-package-001.
Create a postpaid session with idempotency key smoke-start-debt-001 when wallet/package are not used.
Pay a debt with idempotency key smoke-debt-pay-001.
Refund one gameplay charge with idempotency key smoke-refund-001.
Inspect PostgreSQL rows in player_accounts, ledger_entries, billing_command_idempotency, tariffs, tariff_versions, package_definitions, player_packages, sessions, session_command_idempotency, and audit_records.
```

- [ ] **Step 4: Update README and progress**

Update `README.md` current endpoint list with the Phase 5 endpoints from Task 6 and note:

```text
Wallet and debt values are derived from ledger_entries.
There is no mutable balance field.
```

Update progress with implemented Phase 5 items, latest verification commands, known limitations, and local smoke status.

## Task 9: Full Verification And Commit

**Files:**

- Modify: none unless verification exposes a concrete compile, test, or documentation issue.

- [ ] **Step 1: Run targeted Phase 5 tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "BillingContractSerializationTests|TariffContractSerializationTests|PackageContractSerializationTests|SessionContractSerializationTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "LedgerBalanceProjectorTests|EfBillingCommandServiceTests|EfTariffServiceTests|EfPackageServiceTests|BillingEndpointTests|EfSessionBillingIntegrationTests" --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

- [ ] **Step 2: Run full build**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 3: Run full test suite**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Passed! - Failed: 0
```

- [ ] **Step 4: Commit coherent Phase 5 slice**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' status --short
& 'C:\Program Files\Git\cmd\git.exe' add docs src tests README.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add billing ledger tariffs packages foundation"
```

Expected:

```text
[codex/phase5-billing-ledger-tariffs-packages ...] feat: add billing ledger tariffs packages foundation
```

## Plan Self-Review

Spec coverage:

- PRD immutable ledger is covered by Tasks 2, 3, 5, 7, and the explicit no-mutable-balance migration review in Task 8.
- Wallet balance derived from ledger entries is covered by Tasks 2 and 3.
- Prepaid wallet gameplay flow is covered by Task 7.
- Postpaid debt flow and debt payment are covered by Tasks 3, 6, and 7.
- Refunds, reversals, and manual corrections are covered by Task 3 and endpoint coverage in Task 6.
- Package purchase, package consumption, and bonus grants are covered by Task 5 and session integration in Task 7.
- Tariff versioning and calculation foundation is covered by Task 4.
- Money-command idempotency is covered by Tasks 3, 4, 5, 6, and 7.
- Multi-tenant and branch-scoped authorization is covered by Task 6.
- Audit for critical money operations is covered by Task 6.

Out-of-scope checks:

- No POS, product catalog, inventory, sales, returns, receipts, shifts, or cash reconciliation are added.
- No Operator App production UX is added.
- No Agent enforcement or Player Shell UI behavior is changed.
- No web admin panel, local club server, or microservice split is introduced.
- No mutable wallet, debt, package, or bonus balance field is introduced.

Placeholder scan:

- The plan has concrete file paths, DTO names, entity names, endpoint routes, permission names, audit action names, table names, indexes, commands, and expected results.
- Open product decisions are not introduced.
- Deferred scope is tied to named future roadmap phases.

Type consistency:

- `BillingModeNames` values match session billing rules.
- `LedgerEntryTypeNames` and `LedgerAccountTypeNames` values match persistence and projection rules.
- `StaffPermissionNames` additions match `PermissionCatalog` and endpoint authorization.
- `AuditActionNames` additions match endpoint audit writes.
- `TariffCalculationResult.TariffRuleVersionId` matches existing `SessionDto.TariffRuleVersionId`.
- `PlayerPackageId` is used consistently in contracts, ledger rows, package projections, and session package billing.
