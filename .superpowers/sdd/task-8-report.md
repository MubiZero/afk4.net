# Task 8 Execution Report

Date: 2026-07-13

Base: `4041081a5144ea237a37a3dda4385bbf5f440036`

Branch: `feat/commerce-financial-integrity-impl`

## RED / GREEN Evidence

### Sales report COGS contracts and implementation

RED:

```text
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false --filter ReportContractSerializationTests -v minimal
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false --filter "EfReportServiceTests|ReportCsvExporterTests" -v minimal
```

Both commands failed to compile for the intended reason: `SalesReportRowDto`
did not define `GrossCostOfGoods`, `RefundedCostOfGoods`, or `NetCostOfGoods`,
and `SalesReportResultDto` did not define the corresponding totals.

GREEN:

```text
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false --filter ReportContractSerializationTests -v minimal
Passed 6, failed 0, skipped 0.

dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false --filter "EfReportServiceTests|ReportCsvExporterTests" -v minimal
Passed 11, failed 0, skipped 0.
```

The implementation derives COGS only from checked multiplication/summation of
`PosSaleLineEntity.UnitCostMinorUnits`. Refunded sales retain positive gross
COGS, expose a negative refunded COGS, and net to zero. Retail revenue/refund
totals remain payment-price based.

### PostgreSQL last-unit concurrency

RED:

```text
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false --filter ShopCommercePostgresTests -v minimal
```

Compilation failed because `ShopCommercePostgresFixture` did not exist. The
failing test already required two independent placements, one success, one
`out_of_stock`, and the exact persisted finance/stock artifacts.

Explicit gate without environment:

```text
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false --filter ShopCommercePostgresTests -v minimal
Skipped 1: Set AFK4_COMMERCE_TEST_POSTGRES to a PostgreSQL database whose name ends with _test.
```

Real PostgreSQL GREEN:

```text
AFK4_COMMERCE_TEST_POSTGRES='Host=127.0.0.1;Port=<temporary>;Database=afk4_commerce_test;Username=postgres;Password=<temporary>' \
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false --filter ShopCommercePostgresTests -v normal
Passed 1, failed 0, skipped 0; 0 warnings, 0 errors; test duration 3 s.
```

The fixture rejects non-`_test` databases, creates a generated isolated schema,
runs EF migrations, resolves an independent DI scope/DbContext per placement,
and drops only its generated schema. The isolated PostgreSQL 17 container was
stopped and removed after the run; the unrelated ReviewOS PostgreSQL container
was not touched.

### Linked Shop, shift, and projection assertions

```text
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false --filter "BillingShiftIntegrationTests|ShopOrderProjectionTests" -v minimal
Passed 7, failed 0, skipped 0.
```

The assertions prove the linked wallet payment contributes once to the open
shift, the refund subtracts once, and Shop/POS/receipt IDs and retail totals
agree while immutable unit cost remains separate from retail price.

## Aggregate Verification

```text
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
Passed 125, failed 0, skipped 0.

dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false --filter "Shop|Pos|Inventory|BillingShiftIntegrationTests" -v minimal
Passed 262, failed 0, skipped 1 (the explicit PostgreSQL env gate).

dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
Build succeeded, 0 warnings, 0 errors.

dotnet build tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --no-restore -p:EnableWindowsTargeting=true -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
Build succeeded, 0 warnings, 0 errors.

cd src/AFK4.Player.Shell.Web && bun test && bun run build
51 tests passed, 0 failed; production build succeeded.

dotnet restore AFK4.sln -p:EnableWindowsTargeting=true -p:NuGetAudit=false -v minimal
Restore succeeded.

dotnet build AFK4.sln --no-restore -p:EnableWindowsTargeting=true -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
Build succeeded, 0 warnings, 0 errors.
```

Full solution test attempt:

```text
dotnet test AFK4.sln --no-build -p:EnableWindowsTargeting=true -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Portable results included Platform API 1288 passed / 1 PostgreSQL-env skip,
Shared Contracts 125/125, Localization 15/15, Building Blocks 3/3, Setup Wizard
30 passed / 1 environment skip, and Update Publisher 8/8. The command as a
whole was not green on Fedora: Operator App and Player Shell `.NET` testhosts
require the unavailable `Microsoft.WindowsDesktop.App 10.0` Windows runtime;
26 Agent packaging/release tests invoke Windows PowerShell/release tooling and
failed in this Linux environment. These are explicit platform limitations;
all affected Task 8 suites and all projects compile successfully.

Player Shell Web emitted the pre-existing React `act(...)` notices in two
ExtendScreen tests and the existing Vite chunk-size warning; there were no test
or build failures.
