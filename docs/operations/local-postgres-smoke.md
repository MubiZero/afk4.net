# Local PostgreSQL And Device Smoke Runbook

This runbook verifies the current AFK4 identity, audit, layout, device
persistence, session lifecycle, and Phase 5 billing foundation against a real
local PostgreSQL database. It covers EF migration application, staff sign-in,
authorized device enrollment-code creation, device enrollment, authenticated
heartbeat persistence, persisted floor-map reads, immutable ledger writes,
wallet/debt/package projections, tariff version calculation, signed session
leases, session and billing command idempotency, reconnect reconciliation,
installed app reporting, device detail reads, command status storage, and
staff-protected device credential rotation/revocation.

The commands assume PowerShell from the repository root:

```powershell
Set-Location D:\afk4.net
```

## Prerequisites

- Docker Desktop or another Docker runtime with Compose support.
- .NET SDK `10.0.203`.
- Port `5432` available on `127.0.0.1`.
- Port `5074` available for the Platform API.

The Compose file binds PostgreSQL to localhost only and uses trust
authentication for local development. Do not use this Compose profile for
staging or production.

## Start PostgreSQL

```powershell
docker compose up -d postgres
docker compose ps
```

Wait until the `afk4-postgres` health state is `healthy`.

If the database must be reset completely:

```powershell
docker compose down -v
docker compose up -d postgres
```

## Restore EF Tool

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
```

## Apply EF Migrations

The current local connection string matches the Platform API fallback and the
Compose service:

```text
Host=localhost;Port=5432;Database=afk4_dev;Username=postgres
```

Apply the migrations:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' ef database update `
  --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
  --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

If port `5432` is occupied and a temporary PostgreSQL container is bound to a
different localhost port, pass the connection explicitly because the EF
design-time factory uses the local fallback connection:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' ef database update `
  --connection "Host=localhost;Port=55433;Database=afk4_dev;Username=postgres" `
  --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
  --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

## Start Platform API

Generate a local ECDSA key pair for backend session lease signing. Keep the
private key only in local environment/configuration; the Agent receives only
the public key.

```powershell
$leaseSigningKey = [System.Security.Cryptography.ECDsa]::Create(
    [System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
$env:Sessions__SigningPrivateKeyPem = $leaseSigningKey.ExportECPrivateKeyPem()
$env:Agent__LeaseSigningPublicKeyPem = $leaseSigningKey.ExportSubjectPublicKeyInfoPem()
```

On Windows PowerShell versions where `ECDsaCng` does not expose PEM export
helpers, Git for Windows OpenSSL can generate the same temporary key pair:

```powershell
$privateKeyPath = Join-Path $env:TEMP 'afk4-smoke-private.pem'
$publicKeyPath = Join-Path $env:TEMP 'afk4-smoke-public.pem'
& 'C:\Program Files\Git\usr\bin\openssl.exe' ecparam -name prime256v1 -genkey -noout -out $privateKeyPath
& 'C:\Program Files\Git\usr\bin\openssl.exe' ec -in $privateKeyPath -pubout -out $publicKeyPath
$env:Sessions__SigningPrivateKeyPem = Get-Content -Raw -LiteralPath $privateKeyPath
$env:Agent__LeaseSigningPublicKeyPem = Get-Content -Raw -LiteralPath $publicKeyPath
```

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run `
  --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
  --urls http://localhost:5074
```

Keep this process running while executing the smoke commands in another
PowerShell session.

## Live Smoke

Use stable IDs for repeatable smoke requests:

```powershell
$baseUrl = 'http://localhost:5074'
$organizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08'
$branchId = 'acfc0212-967f-4d84-94be-9003387b09c2'
```

Verify health:

```powershell
Invoke-RestMethod "$baseUrl/api/health"
```

Seed a local branch manager staff user for the smoke run. The seeded password
is `Passw0rd!` and the password hash is for local development only:

```powershell
$staffUserId = '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134'
$staffRoleAssignmentId = '58e8a836-82cd-45d1-a0cc-c13621e76c4e'
$passwordHash = 'AQAAAAIAAYagAAAAEBtg5uNEqBhvMLTcq8WPczYLamzC17d4URzbuoedWQV8HBZPONhd1Wapb1t6X/wKag=='

$seedSql = @"
INSERT INTO organizations ("OrganizationId", "Name", "CreatedAtUtc")
VALUES ('$organizationId', 'Demo Organization', now())
ON CONFLICT ("OrganizationId")
DO UPDATE SET "Name" = EXCLUDED."Name";

INSERT INTO branches ("BranchId", "OrganizationId", "Name", "CreatedAtUtc")
VALUES ('$branchId', '$organizationId', 'Demo Branch', now())
ON CONFLICT ("BranchId")
DO UPDATE SET "OrganizationId" = EXCLUDED."OrganizationId",
              "Name" = EXCLUDED."Name";

INSERT INTO staff_users (
    "StaffUserId",
    "OrganizationId",
    "UserName",
    "NormalizedUserName",
    "DisplayName",
    "PasswordHash",
    "IsActive",
    "CreatedAtUtc")
VALUES (
    '$staffUserId',
    '$organizationId',
    'tech@afk4.test',
    'TECH@AFK4.TEST',
    'Smoke Branch Manager',
    '$passwordHash',
    true,
    now())
ON CONFLICT ("OrganizationId", "NormalizedUserName")
DO UPDATE SET "DisplayName" = EXCLUDED."DisplayName",
              "PasswordHash" = EXCLUDED."PasswordHash",
              "IsActive" = true;

INSERT INTO staff_role_assignments (
    "StaffRoleAssignmentId",
    "StaffUserId",
    "OrganizationId",
    "BranchId",
    "RoleName")
VALUES (
    '$staffRoleAssignmentId',
    '$staffUserId',
    '$organizationId',
    '$branchId',
    'branch_manager')
ON CONFLICT ("StaffUserId", "OrganizationId", "BranchId", "RoleName")
DO NOTHING;
"@

$seedSql | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Sign in as the local branch manager:

```powershell
$signInBody = @{
    organizationId = $organizationId
    userName = 'tech@afk4.test'
    password = 'Passw0rd!'
} | ConvertTo-Json -Depth 4

$staffSession = Invoke-RestMethod `
    "$baseUrl/api/auth/staff/sign-in" `
    -Method Post `
    -ContentType 'application/json' `
    -Body $signInBody

$staffHeaders = @{
    Authorization = "Bearer $($staffSession.accessToken)"
}
```

Rotate the refresh token once and continue with the refreshed access token:

```powershell
$refreshBody = @{
    organizationId = $organizationId
    refreshToken = $staffSession.refreshToken
} | ConvertTo-Json -Depth 4

$refreshedStaffSession = Invoke-RestMethod `
    "$baseUrl/api/auth/staff/refresh" `
    -Method Post `
    -ContentType 'application/json' `
    -Body $refreshBody

$staffHeaders = @{
    Authorization = "Bearer $($refreshedStaffSession.accessToken)"
}
```

Create an enrollment code with the staff bearer token:

```powershell
$codeBody = @{
    organizationId = $organizationId
    expiresInSeconds = 300
} | ConvertTo-Json -Depth 4

$code = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/device-enrollment-codes" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $codeBody
```

Enroll a device:

```powershell
$enrollBody = @{
    organizationId = $organizationId
    branchId = $branchId
    enrollmentCode = $code.code
    machineName = 'PC-SMOKE-001'
    agentVersion = '0.1.0'
    shellVersion = '0.1.0'
    requestedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
} | ConvertTo-Json -Depth 4

$enrollment = Invoke-RestMethod `
    "$baseUrl/api/devices/enroll" `
    -Method Post `
    -ContentType 'application/json' `
    -Body $enrollBody
```

Post an authenticated heartbeat:

```powershell
$heartbeatBody = @{
    organizationId = $organizationId
    branchId = $branchId
    deviceId = $enrollment.deviceId
    machineName = 'PC-SMOKE-001'
    agentVersion = '0.1.0'
    shellVersion = '0.1.0'
    observedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
    isLocked = $true
} | ConvertTo-Json -Depth 4

$heartbeat = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/heartbeat" `
    -Method Post `
    -Headers @{ 'X-AFK4-Device-Credential' = $enrollment.credentialSecret } `
    -ContentType 'application/json' `
    -Body $heartbeatBody
```

Seed one zone, one seat, and an active device-seat assignment for the enrolled
device:

```powershell
$zoneId = '2e37f7b3-41bb-4a19-9d50-94eb848f4e01'
$seatId = '9f3adbd3-957e-4dc8-8d34-a6bfa56b9275'
$assignmentId = 'ad8c15f4-7ff1-44b4-9f9e-f27e2f0c1b44'

@"
INSERT INTO zones ("ZoneId", "OrganizationId", "BranchId", "Name", "SortOrder", "CreatedAtUtc")
VALUES ('$zoneId', '$organizationId', '$branchId', 'Main Hall', 10, now())
ON CONFLICT ("ZoneId")
DO UPDATE SET "Name" = EXCLUDED."Name",
              "SortOrder" = EXCLUDED."SortOrder";

INSERT INTO seats ("SeatId", "OrganizationId", "BranchId", "ZoneId", "Name", "SortOrder", "CreatedAtUtc")
VALUES ('$seatId', '$organizationId', '$branchId', '$zoneId', 'PC-SMOKE-001', 10, now())
ON CONFLICT ("SeatId")
DO UPDATE SET "ZoneId" = EXCLUDED."ZoneId",
              "Name" = EXCLUDED."Name",
              "SortOrder" = EXCLUDED."SortOrder";

UPDATE device_seat_assignments
SET "DetachedAtUtc" = now()
WHERE "DeviceId" = '$($enrollment.deviceId)'
  AND "DetachedAtUtc" IS NULL;

INSERT INTO device_seat_assignments (
    "DeviceSeatAssignmentId",
    "OrganizationId",
    "BranchId",
    "SeatId",
    "DeviceId",
    "AttachedAtUtc",
    "DetachedAtUtc")
VALUES (
    '$assignmentId',
    '$organizationId',
    '$branchId',
    '$seatId',
    '$($enrollment.deviceId)',
    now(),
    NULL)
ON CONFLICT ("DeviceSeatAssignmentId")
DO UPDATE SET "SeatId" = EXCLUDED."SeatId",
              "DeviceId" = EXCLUDED."DeviceId",
              "DetachedAtUtc" = NULL;
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Read the staff-protected persisted floor map:

```powershell
$floorMap = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/floor-map" `
    -Headers $staffHeaders
```

Create a player account for Phase 5 billing smoke:

```powershell
$playerBody = @{
    organizationId = $organizationId
    displayName = 'Smoke Player'
    phoneNumber = '+992000000001'
    idempotencyKey = 'smoke-player-001'
} | ConvertTo-Json -Depth 6

$player = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/players" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $playerBody

$playerAccountId = $player.playerAccountId
```

Open an operator shift. Keep this shift open until the end of the smoke run so
Phase 6 POS activity and Phase 5 money-changing billing commands can link to
the same shift for reconciliation:

```powershell
$openShiftBody = @{
    organizationId = $organizationId
    startingCash = @{
        currencyCode = 'TJS'
        minorUnits = 50000
    }
    openingNote = 'local-postgres-smoke opening cash'
    idempotencyKey = 'smoke-shift-open-001'
} | ConvertTo-Json -Depth 8

$shift = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/shifts/open" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $openShiftBody

$shiftId = $shift.shiftId

$currentShift = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/shifts/current" `
    -Headers $staffHeaders
```

Create a POS category and product, append purchase stock, and read derived
stock on hand from the catalog:

```powershell
$categoryBody = @{
    organizationId = $organizationId
    name = 'Smoke Drinks'
    idempotencyKey = 'smoke-pos-category-001'
} | ConvertTo-Json -Depth 6

$category = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/pos/categories" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $categoryBody

$productBody = @{
    organizationId = $organizationId
    categoryId = $category.categoryId
    name = 'Smoke Cola 0.5'
    sku = 'SMOKE-COLA-05'
    price = @{
        currencyCode = 'TJS'
        minorUnits = 1200
    }
    trackStock = $true
    allowNegativeStock = $false
    idempotencyKey = 'smoke-pos-product-001'
} | ConvertTo-Json -Depth 8

$product = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/pos/products" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $productBody

$stockBody = @{
    organizationId = $organizationId
    productId = $product.productId
    movementType = 'purchase'
    quantityDelta = 24
    unitCost = @{
        currencyCode = 'TJS'
        minorUnits = 900
    }
    reason = 'local-postgres-smoke initial stock'
    idempotencyKey = 'smoke-stock-001'
} | ConvertTo-Json -Depth 8

$purchaseStock = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/inventory/stock-movements" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $stockBody

$catalog = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/pos/catalog" `
    -Headers $staffHeaders
```

Create a POS sale, pay it with the manual cash provider, inspect the paid sale
and receipt, then refund the sale:

```powershell
$saleBody = @{
    organizationId = $organizationId
    shiftId = $shiftId
    lines = @(
        @{
            productId = $product.productId
            productName = ''
            quantity = 2
            unitPrice = @{
                currencyCode = 'TJS'
                minorUnits = 0
            }
            lineTotal = @{
                currencyCode = 'TJS'
                minorUnits = 0
            }
        }
    )
    idempotencyKey = 'smoke-pos-sale-001'
} | ConvertTo-Json -Depth 10

$sale = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/pos/sales" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $saleBody

$paymentBody = @{
    organizationId = $organizationId
    paymentMethod = 'cash'
    amount = @{
        currencyCode = 'TJS'
        minorUnits = 2400
    }
    note = 'local-postgres-smoke cash drawer'
    idempotencyKey = 'smoke-pos-pay-001'
} | ConvertTo-Json -Depth 8

$paidSale = Invoke-RestMethod `
    "$baseUrl/api/pos/sales/$($sale.posSaleId)/payments/manual" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $paymentBody

$readSale = Invoke-RestMethod `
    "$baseUrl/api/pos/sales/$($sale.posSaleId)" `
    -Headers $staffHeaders

$receiptId = @"
SELECT "ReceiptId"
FROM receipts
WHERE "PosSaleId" = '$($sale.posSaleId)'
  AND "ReceiptType" = 'sale'
LIMIT 1;
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev -t -A
$receiptId = $receiptId.Trim()

$receipt = Invoke-RestMethod `
    "$baseUrl/api/receipts/$receiptId" `
    -Headers $staffHeaders

$refundBody = @{
    organizationId = $organizationId
    reason = 'local-postgres-smoke customer return'
    idempotencyKey = 'smoke-pos-refund-001'
} | ConvertTo-Json -Depth 6

$refundedSale = Invoke-RestMethod `
    "$baseUrl/api/pos/sales/$($sale.posSaleId)/refunds" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $refundBody
```

Top up the wallet with a stable idempotency key:

```powershell
$topUpBody = @{
    organizationId = $organizationId
    amount = @{
        currencyCode = 'TJS'
        minorUnits = 100000
    }
    reason = 'local-postgres-smoke wallet preload'
    idempotencyKey = 'smoke-topup-001'
} | ConvertTo-Json -Depth 8

$topUp = Invoke-RestMethod `
    "$baseUrl/api/players/$playerAccountId/wallet/top-ups" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $topUpBody

$repeatedTopUp = Invoke-RestMethod `
    "$baseUrl/api/players/$playerAccountId/wallet/top-ups" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $topUpBody
```

Append a small manual wallet correction to exercise the protected correction
path:

```powershell
$manualCorrectionBody = @{
    organizationId = $organizationId
    accountType = 'wallet'
    amount = @{
        currencyCode = 'TJS'
        minorUnits = 100
    }
    quantitySeconds = 0
    reason = 'local-postgres-smoke manual wallet correction'
    idempotencyKey = 'smoke-manual-correction-001'
} | ConvertTo-Json -Depth 8

$manualCorrection = Invoke-RestMethod `
    "$baseUrl/api/players/$playerAccountId/ledger/manual-corrections" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $manualCorrectionBody
```

Create a tariff and version, then calculate 60 minutes:

```powershell
$tariffBody = @{
    organizationId = $organizationId
    name = 'Smoke Hourly'
    idempotencyKey = 'smoke-tariff-001'
} | ConvertTo-Json -Depth 6

$tariff = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/tariffs" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $tariffBody

$tariffVersionBody = @{
    organizationId = $organizationId
    tariffId = $tariff.tariffId
    currencyCode = 'TJS'
    pricePerMinuteMinorUnits = 100
    minimumBillableMinutes = 1
    roundingIncrementMinutes = 1
    effectiveFromUtc = (Get-Date).ToUniversalTime().AddMinutes(-1).ToString('O')
    idempotencyKey = 'smoke-tariff-version-001'
} | ConvertTo-Json -Depth 8

$tariffVersion = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/tariffs/$($tariff.tariffId)/versions" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $tariffVersionBody

$tariffCalculationBody = @{
    organizationId = $organizationId
    tariffVersionId = $tariffVersion.tariffVersionId
    durationMinutes = 60
} | ConvertTo-Json -Depth 6

$tariffCalculation = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/tariffs/calculate" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $tariffCalculationBody
```

Start a prepaid wallet session on the assigned seat with a stable idempotency
key:

```powershell
$startSessionBody = @{
    organizationId = $organizationId
    seatId = $seatId
    durationMinutes = 60
    tariffRuleVersionId = $tariffCalculation.tariffRuleVersionId
    idempotencyKey = 'smoke-start-prepaid-001'
    playerAccountId = $playerAccountId
    billingMode = 'prepaid_wallet'
    tariffVersionId = $tariffVersion.tariffVersionId
} | ConvertTo-Json -Depth 6

$startedSession = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/sessions/start" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $startSessionBody
```

Repeat the same start request and confirm the idempotent response returns the
same `sessionId`:

```powershell
$repeatedStartSession = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/sessions/start" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $startSessionBody

if ($startedSession.session.sessionId -ne $repeatedStartSession.session.sessionId) {
    throw 'Repeated start did not return the original sessionId.'
}
```

Create a package definition, purchase it, and extend the active session with
package-backed time using a stable idempotency key:

```powershell
$packageDefinitionBody = @{
    organizationId = $organizationId
    name = 'Smoke 2h Pack'
    price = @{
        currencyCode = 'TJS'
        minorUnits = 30000
    }
    includedSeconds = 7200
    bonusSeconds = 600
    expiresAfterDays = 30
    idempotencyKey = 'smoke-package-def-001'
} | ConvertTo-Json -Depth 8

$packageDefinition = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/packages" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $packageDefinitionBody

$packagePurchaseBody = @{
    organizationId = $organizationId
    packageDefinitionId = $packageDefinition.packageDefinitionId
    idempotencyKey = 'smoke-package-buy-001'
} | ConvertTo-Json -Depth 6

$playerPackage = Invoke-RestMethod `
    "$baseUrl/api/players/$playerAccountId/packages/purchases" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $packagePurchaseBody

$sessionId = $startedSession.session.sessionId
$packageExtendBody = @{
    additionalMinutes = 15
    tariffRuleVersionId = "package:$($playerPackage.playerPackageId)"
    idempotencyKey = 'smoke-start-package-001'
    playerAccountId = $playerAccountId
    billingMode = 'package'
    playerPackageId = $playerPackage.playerPackageId
} | ConvertTo-Json -Depth 8

$packageExtendedSession = Invoke-RestMethod `
    "$baseUrl/api/sessions/$sessionId/extend" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $packageExtendBody

$playerPackages = Invoke-RestMethod `
    "$baseUrl/api/players/$playerAccountId/packages" `
    -Headers $staffHeaders
```

Extend the session once more with prepaid wallet billing:

```powershell
$extendSessionBody = @{
    additionalMinutes = 15
    tariffRuleVersionId = $tariffCalculation.tariffRuleVersionId
    idempotencyKey = 'smoke-extend-001'
    playerAccountId = $playerAccountId
    billingMode = 'prepaid_wallet'
    tariffVersionId = $tariffVersion.tariffVersionId
} | ConvertTo-Json -Depth 8

$extendedSession = Invoke-RestMethod `
    "$baseUrl/api/sessions/$sessionId/extend" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $extendSessionBody

$activeLease = $extendedSession.session.currentLease
```

Create a postpaid-debt session with idempotency key
`smoke-start-debt-001` in a fresh database or on another available assigned
seat/device. Phase 4 currently moves ended sessions to `ending` and leaves final
Agent acknowledgement/completion for a later slice, so the same seat should not
be reused for a second start in a single smoke run.

```powershell
$postpaidCode = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/device-enrollment-codes" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $codeBody

$postpaidEnrollBody = @{
    organizationId = $organizationId
    branchId = $branchId
    enrollmentCode = $postpaidCode.code
    machineName = 'PC-SMOKE-002'
    agentVersion = '0.1.0'
    shellVersion = '0.1.0'
    requestedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
} | ConvertTo-Json -Depth 4

$postpaidEnrollment = Invoke-RestMethod `
    "$baseUrl/api/devices/enroll" `
    -Method Post `
    -ContentType 'application/json' `
    -Body $postpaidEnrollBody

$postpaidSeatId = '5d42fdcf-fd80-47b6-971c-f705702f4730'
$postpaidAssignmentId = 'b61ef7ca-36ac-4c60-a29d-b6936cb1a1a9'

@"
INSERT INTO seats ("SeatId", "OrganizationId", "BranchId", "ZoneId", "Name", "SortOrder", "CreatedAtUtc")
VALUES ('$postpaidSeatId', '$organizationId', '$branchId', '$zoneId', 'PC-SMOKE-002', 20, now())
ON CONFLICT ("SeatId")
DO UPDATE SET "ZoneId" = EXCLUDED."ZoneId",
              "Name" = EXCLUDED."Name",
              "SortOrder" = EXCLUDED."SortOrder";

INSERT INTO device_seat_assignments (
    "DeviceSeatAssignmentId",
    "OrganizationId",
    "BranchId",
    "SeatId",
    "DeviceId",
    "AttachedAtUtc",
    "DetachedAtUtc")
VALUES (
    '$postpaidAssignmentId',
    '$organizationId',
    '$branchId',
    '$postpaidSeatId',
    '$($postpaidEnrollment.deviceId)',
    now(),
    NULL);
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev

$postpaidStartBody = @{
    organizationId = $organizationId
    seatId = $postpaidSeatId
    durationMinutes = 30
    tariffRuleVersionId = $tariffCalculation.tariffRuleVersionId
    idempotencyKey = 'smoke-start-debt-001'
    playerAccountId = $playerAccountId
    billingMode = 'postpaid_debt'
    tariffVersionId = $tariffVersion.tariffVersionId
} | ConvertTo-Json -Depth 8

$postpaidSession = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/sessions/start" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $postpaidStartBody

$debtPaymentBody = @{
    organizationId = $organizationId
    amount = @{
        currencyCode = 'TJS'
        minorUnits = 3000
    }
    reason = 'local-postgres-smoke debt payment'
    idempotencyKey = 'smoke-debt-pay-001'
} | ConvertTo-Json -Depth 8

$debtPayment = Invoke-RestMethod `
    "$baseUrl/api/players/$playerAccountId/debts/payments" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $debtPaymentBody
```

Refund the prepaid wallet gameplay charge:

```powershell
$walletSummary = Invoke-RestMethod `
    "$baseUrl/api/players/$playerAccountId/wallet-summary" `
    -Headers $staffHeaders

$gameplayChargeEntry = $walletSummary.recentEntries |
    Where-Object { $_.entryType -eq 'gameplay_charge' -and $_.accountType -eq 'wallet' } |
    Select-Object -First 1

if ($null -eq $gameplayChargeEntry) {
    throw 'No prepaid wallet gameplay charge found to refund.'
}

$refundBody = @{
    organizationId = $organizationId
    ledgerEntryId = $gameplayChargeEntry.ledgerEntryId
    amount = @{
        currencyCode = 'TJS'
        minorUnits = [Math]::Abs([int64]$gameplayChargeEntry.amount.minorUnits)
    }
    reason = 'local-postgres-smoke gameplay refund'
    idempotencyKey = 'smoke-refund-001'
} | ConvertTo-Json -Depth 8

$refund = Invoke-RestMethod `
    "$baseUrl/api/players/$playerAccountId/ledger/$($gameplayChargeEntry.ledgerEntryId)/refunds" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $refundBody
```

Report reconciliation while the cloud session is active. A matching local lease
should return `continue`:

```powershell
$reconciliationBody = @{
    organizationId = $organizationId
    branchId = $branchId
    deviceId = $enrollment.deviceId
    activeSessionId = $sessionId
    activeLease = $activeLease
    isLocked = $false
    pendingLocalEventCount = 0
    observedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
} | ConvertTo-Json -Depth 12

$activeReconciliation = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/session-reconciliation" `
    -Method Post `
    -Headers @{ 'X-AFK4-Device-Credential' = $enrollment.credentialSecret } `
    -ContentType 'application/json' `
    -Body $reconciliationBody
```

If the smoke setup seeds a second enrolled device and assigned seat, transfer
the session by posting to `POST /api/sessions/{sessionId}/transfer` with:

```powershell
$transferBody = @{
    targetSeatId = $secondSeatId
    idempotencyKey = 'smoke-transfer-001'
} | ConvertTo-Json -Depth 4

$transferredSession = Invoke-RestMethod `
    "$baseUrl/api/sessions/$sessionId/transfer" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $transferBody
```

End the session and then reconcile the still-local lease. Because the cloud
session is ending, reconciliation should return `lock`:

```powershell
$endSessionBody = @{
    reason = 'local-postgres-smoke'
    idempotencyKey = 'smoke-end-001'
} | ConvertTo-Json -Depth 6

$endingSession = Invoke-RestMethod `
    "$baseUrl/api/sessions/$sessionId/end" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $endSessionBody

$endingReconciliation = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/session-reconciliation" `
    -Method Post `
    -Headers @{ 'X-AFK4-Device-Credential' = $enrollment.credentialSecret } `
    -ContentType 'application/json' `
    -Body $reconciliationBody
```

Post an authenticated installed apps report from the device:

```powershell
$installedAppsBody = @{
    organizationId = $organizationId
    branchId = $branchId
    deviceId = $enrollment.deviceId
    reportedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
    apps = @(
        @{
            displayName = 'Counter-Strike 2'
            version = '2.0.0'
            publisher = 'Valve'
            installLocation = 'C:\Games\Counter-Strike 2'
            installedAtUtc = '2026-05-01T08:30:00Z'
        },
        @{
            displayName = 'Discord'
            version = '1.0.9059'
            publisher = 'Discord Inc.'
            installLocation = $null
            installedAtUtc = $null
        }
    )
} | ConvertTo-Json -Depth 8

Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/installed-apps/report" `
    -Method Post `
    -Headers @{ 'X-AFK4-Device-Credential' = $enrollment.credentialSecret } `
    -ContentType 'application/json' `
    -Body $installedAppsBody
```

Read the staff-protected device detail projection:

```powershell
$deviceDetail = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)" `
    -Headers $staffHeaders
```

Create a device command and read its persisted status:

```powershell
$commandBody = @{
    type = 'lock'
    payload = @{
        reason = 'local-postgres-smoke'
    }
} | ConvertTo-Json -Depth 4

$command = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/commands" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $commandBody

$status = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/commands/$($command.commandId)/status" `
    -Headers $staffHeaders
```

Report the command result through the device-authenticated HTTP fallback used
when the realtime hub is unavailable:

```powershell
$commandResultBody = @{
    organizationId = $organizationId
    branchId = $branchId
    deviceId = $enrollment.deviceId
    commandId = $command.commandId
    status = 'Accepted'
    message = 'handled from local-postgres-smoke'
    observedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
} | ConvertTo-Json -Depth 6

$commandResult = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/commands/$($command.commandId)/result" `
    -Method Post `
    -Headers @{ 'X-AFK4-Device-Credential' = $enrollment.credentialSecret } `
    -ContentType 'application/json' `
    -Body $commandResultBody

$statusAfterResult = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/commands/$($command.commandId)/status" `
    -Headers $staffHeaders
```

Rotate the device credential, then verify the new secret can authenticate a
heartbeat:

```powershell
$rotatedCredential = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/credentials/rotate" `
    -Method Post `
    -Headers $staffHeaders

$rotatedHeartbeat = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/heartbeat" `
    -Method Post `
    -Headers @{ 'X-AFK4-Device-Credential' = $rotatedCredential.credentialSecret } `
    -ContentType 'application/json' `
    -Body $heartbeatBody
```

Revoke the rotated credential:

```powershell
$revokedCredential = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/credentials/$($rotatedCredential.credentialId)/revoke" `
    -Method Post `
    -Headers $staffHeaders
```

Record a final cash movement and close the shift. The counted cash below
assumes the postpaid debt payment path above was run; subtract `3000` from the
counted amount if that optional second-seat path is skipped:

```powershell
$cashMovementBody = @{
    organizationId = $organizationId
    movementType = 'cash_in'
    amount = @{
        currencyCode = 'TJS'
        minorUnits = 5000
    }
    reason = 'local-postgres-smoke cash drawer refill'
    idempotencyKey = 'smoke-cash-in-001'
} | ConvertTo-Json -Depth 8

$cashMovement = Invoke-RestMethod `
    "$baseUrl/api/shifts/$shiftId/cash-movements" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $cashMovementBody

$closeShiftBody = @{
    organizationId = $organizationId
    countedCash = @{
        currencyCode = 'TJS'
        minorUnits = 158100
    }
    closingNote = 'local-postgres-smoke balanced close'
    idempotencyKey = 'smoke-shift-close-001'
} | ConvertTo-Json -Depth 8

$closedShift = Invoke-RestMethod `
    "$baseUrl/api/shifts/$shiftId/close" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $closeShiftBody
```

Expected results:

- health returns `status = ok`;
- staff sign-in returns non-empty `accessToken` and `refreshToken`, and
  includes `devices.enrollment_codes.create`, `devices.credentials.rotate`,
  `devices.credentials.revoke`, `sessions.start`, `sessions.extend`,
  `sessions.transfer`, `sessions.end`, `players.create`, `billing.view`,
  `billing.wallet.top_up`, `billing.refund`, `billing.manual_correction`,
  `billing.debt.pay`, `tariffs.manage`, `packages.manage`, and
  `packages.purchase`, `shifts.open`, `shifts.close`, `shifts.view`,
  `shifts.cash.manage`, `pos.catalog.manage`, `pos.sales.create`,
  `pos.sales.pay`, `pos.sales.refund`, `pos.sales.void`,
  `inventory.stock.manage`, `inventory.view`, and `receipts.view` in
  `permissions`;
- refresh returns a new non-empty `accessToken` and `refreshToken`;
- enrollment returns non-empty `deviceId`, `credentialId`, and
  `credentialSecret`;
- heartbeat returns `heartbeatIntervalSeconds = 10`;
- floor map returns the seeded `PC-SMOKE-001` seat with `zoneName = Main Hall`
  and the enrolled `deviceId`;
- player creation returns a non-empty `playerAccountId`;
- shift open returns state `open`, and current shift returns the same
  `shiftId`;
- POS category and product creation return non-empty IDs;
- purchase stock movement with `smoke-stock-001` appends quantity `24`;
- catalog returns `Smoke Cola 0.5` with derived `stockOnHand = 24`;
- POS sale creation with `smoke-pos-sale-001` returns state `draft`;
- manual cash payment with `smoke-pos-pay-001` returns state `paid`, creates a
  POS receipt number beginning with `POS-`, and writes a negative sale stock
  movement;
- POS refund with `smoke-pos-refund-001` returns state `refunded`, creates a
  refund receipt number beginning with `REF-`, and writes a positive refund
  stock movement;
- top-up with `smoke-topup-001` appends one wallet ledger entry, and repeating
  the same request returns the same idempotent response;
- manual correction with `smoke-manual-correction-001` appends one immutable
  wallet correction entry;
- tariff creation and version creation return non-empty tariff IDs;
- tariff calculation for 60 minutes returns the expected branch tariff version
  and a positive amount;
- prepaid wallet session start returns an active session with a non-empty
  signed `currentLease`;
- repeated prepaid start with `smoke-start-prepaid-001` returns the same
  `sessionId`;
- package definition and package purchase return non-empty IDs;
- package-backed extension with `smoke-start-package-001` returns a refreshed
  signed lease and reduces remaining package seconds in the derived package
  projection;
- prepaid session extend returns a refreshed signed lease;
- postpaid start with `smoke-start-debt-001`, when run on an available second
  seat or fresh database, appends debt ledger entries;
- debt payment with `smoke-debt-pay-001` appends a debt-payment ledger entry;
- refund with `smoke-refund-001` appends a refund ledger entry reversing the
  prepaid gameplay charge;
- active reconciliation returns `action = continue`;
- session end returns `state = ending`;
- ending reconciliation returns `action = lock`;
- installed apps report returns no content and persists two app snapshot rows;
- device detail returns `machineName = PC-SMOKE-001`, assigned seat
  `PC-SMOKE-001`, `activeCredentialCount = 1`, and `installedAppCount = 2`;
- command status initially returns `status = Pending` and `type = lock`;
- command result fallback updates command status to `Accepted`;
- rotation returns a new non-empty `credentialId` and `credentialSecret`;
- heartbeat with the rotated credential returns `heartbeatIntervalSeconds = 10`;
- revocation returns the rotated `credentialId` and a non-empty
  `revokedAtUtc`.
- cash movement with `smoke-cash-in-001` appends one `cash_in` row;
- shift close with `smoke-shift-close-001` returns state `closed`, expected
  cash `158100`, counted cash `158100`, and difference `0` when the full smoke
  path is run.

Optionally inspect recent audit records for the protected staff actions:

```powershell
@'
SELECT "Action", "Outcome", "TargetId"
FROM audit_records
WHERE "Action" IN (
    'devices.enrollment_codes.create',
    'devices.commands.dispatch',
    'devices.commands.status.view',
    'devices.credentials.rotate',
    'devices.credentials.revoke',
    'players.create',
    'billing.wallet.top_up',
    'billing.refund',
    'billing.manual_correction',
    'billing.debt.pay',
    'tariffs.create',
    'tariffs.versions.create',
    'packages.create',
    'packages.purchase',
    'shifts.open',
    'shifts.cash_movement',
    'shifts.close',
    'pos.categories.create',
    'pos.products.create',
    'inventory.stock.create',
    'pos.sales.create',
    'pos.sales.pay',
    'pos.sales.refund',
    'pos.sales.void',
    'sessions.start',
    'sessions.extend',
    'sessions.transfer',
    'sessions.end')
ORDER BY "CreatedAtUtc" DESC
LIMIT 5;
'@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Expected:

```text
devices.credentials.revoke       | Succeeded | ...
devices.credentials.rotate       | Succeeded | ...
packages.purchase                | Succeeded | ...
packages.create                  | Succeeded | ...
shifts.close                     | Succeeded | ...
shifts.cash_movement             | Succeeded | ...
pos.sales.refund                 | Succeeded | ...
pos.sales.pay                    | Succeeded | ...
pos.sales.create                 | Succeeded | ...
inventory.stock.create           | Succeeded | ...
pos.products.create              | Succeeded | ...
pos.categories.create            | Succeeded | ...
shifts.open                      | Succeeded | ...
tariffs.versions.create          | Succeeded | ...
tariffs.create                   | Succeeded | ...
billing.refund                   | Succeeded | ...
billing.wallet.top_up            | Succeeded | ...
players.create                   | Succeeded | ...
devices.commands.status.view     | Succeeded | ...
devices.commands.dispatch        | Succeeded | ...
sessions.end                     | Succeeded | ...
sessions.extend                  | Succeeded | ...
sessions.start                   | Succeeded | ...
devices.enrollment_codes.create  | Succeeded | AFK4-...
```

Optionally inspect the Phase 4 session rows:

```powershell
@"
SELECT "SessionId", "State", "SeatId", "DeviceId", "TariffRuleVersionId"
FROM sessions
WHERE "SessionId" = '$sessionId';

SELECT "SessionId", "Sequence", "ExpiresAtUtc", length("Signature") AS "SignatureLength"
FROM session_leases
WHERE "SessionId" = '$sessionId'
ORDER BY "Sequence";

SELECT "EventType", "DeviceId"
FROM session_events
WHERE "SessionId" = '$sessionId'
ORDER BY "CreatedAtUtc";

SELECT "Operation", "ExpiresAtUtc"
FROM session_command_idempotency
WHERE "BranchId" = '$branchId'
ORDER BY "CreatedAtUtc";

SELECT "Type", "Status", "PayloadJson"
FROM device_commands
WHERE "DeviceId" = '$($enrollment.deviceId)'
ORDER BY "CreatedAtUtc";
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Expected:

```text
sessions: one row with state ending after the end request
session_leases: at least two signed lease rows with increasing Sequence values
session_events: session-started, session-extended, device-reconciled, session-ending
session_command_idempotency: prepaid start, package-backed extend, prepaid
extend, end, and optional postpaid start rows
device_commands: unlock, refresh-session-lease, and lock commands
```

Optionally inspect the Phase 5 billing rows:

```powershell
@"
SELECT "PlayerAccountId", "DisplayName", "HomeBranchId"
FROM player_accounts
WHERE "PlayerAccountId" = '$playerAccountId';

SELECT "EntryType", "AccountType", "AmountMinorUnits", "QuantitySeconds",
       "SessionId", "PlayerPackageId", "ReversesLedgerEntryId"
FROM ledger_entries
WHERE "PlayerAccountId" = '$playerAccountId'
ORDER BY "CreatedAtUtc";

SELECT "Operation", "ExpiresAtUtc"
FROM billing_command_idempotency
WHERE "BranchId" = '$branchId'
ORDER BY "CreatedAtUtc";

SELECT "Name", "IsActive"
FROM tariffs
WHERE "BranchId" = '$branchId';

SELECT "VersionNumber", "PricePerMinuteMinorUnits",
       "MinimumBillableMinutes", "RoundingIncrementMinutes"
FROM tariff_versions
WHERE "TariffId" = '$($tariff.tariffId)'
ORDER BY "VersionNumber";

SELECT "Name", "PriceMinorUnits", "IncludedSeconds", "BonusSeconds"
FROM package_definitions
WHERE "PackageDefinitionId" = '$($packageDefinition.packageDefinitionId)';

SELECT "PlayerPackageId", "IncludedSeconds", "BonusSeconds", "ExpiresAtUtc"
FROM player_packages
WHERE "PlayerPackageId" = '$($playerPackage.playerPackageId)';
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Expected:

```text
player_accounts: one active smoke player in the branch
ledger_entries: immutable top_up, manual_correction, gameplay_charge,
package_purchase, package_consumption or bonus_consumption, and refund rows
billing_command_idempotency: rows for smoke-player-001, smoke-topup-001,
smoke-manual-correction-001, smoke-tariff-001, smoke-tariff-version-001,
smoke-package-def-001, smoke-package-buy-001, smoke-debt-pay-001 when the
postpaid path is run, and smoke-refund-001
tariffs/tariff_versions: Smoke Hourly version 1
package_definitions/player_packages: Smoke 2h Pack purchase rows
```

Optionally inspect the Phase 6 shift, POS, receipt, payment, and inventory rows:

```powershell
@"
SELECT "ShiftId", "State", "ExpectedCashMinorUnits",
       "CountedCashMinorUnits", "DifferenceMinorUnits"
FROM shifts
WHERE "ShiftId" = '$shiftId';

SELECT "MovementType", "AmountMinorUnits"
FROM cash_movements
WHERE "ShiftId" = '$shiftId'
ORDER BY "CreatedAtUtc";

SELECT "Name", "Sku", "TrackStock", "AllowNegativeStock"
FROM pos_products
WHERE "ProductId" = '$($product.productId)';

SELECT "State", "TotalMinorUnits", "ShiftId"
FROM pos_sales
WHERE "PosSaleId" = '$($sale.posSaleId)';

SELECT "ProductName", "Quantity", "UnitPriceMinorUnits", "LineTotalMinorUnits"
FROM pos_sale_lines
WHERE "PosSaleId" = '$($sale.posSaleId)';

SELECT "PaymentKind", "PaymentMethod", "AmountMinorUnits"
FROM payments
WHERE "PosSaleId" = '$($sale.posSaleId)'
ORDER BY "CreatedAtUtc";

SELECT "ReceiptNumber", "ReceiptType", "TotalMinorUnits"
FROM receipts
WHERE "PosSaleId" = '$($sale.posSaleId)'
ORDER BY "CreatedAtUtc";

SELECT "MovementType", "QuantityDelta"
FROM stock_movements
WHERE "ProductId" = '$($product.productId)'
ORDER BY "CreatedAtUtc";
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Expected:

```text
shifts: closed row with expected/count/difference matching the close response
cash_movements: one cash_in row for 5000
pos_products: Smoke Cola 0.5 with TrackStock = true
pos_sales: refunded row linked to the opened shift
pos_sale_lines: one snapshot row with quantity 2 and 1200/2400 minor units
payments: cash payment 2400 and cash refund -2400
receipts: POS-... sale receipt and REF-... refund receipt
stock_movements: purchase +24, sale -2, refund +2
```

Optionally inspect the Phase 3 layout and installed app rows:

```powershell
@"
SELECT z."Name" AS "ZoneName",
       s."Name" AS "SeatName",
       d."DeviceId",
       d."MachineName",
       d."IsOnline",
       d."IsLocked"
FROM seats s
JOIN zones z ON z."ZoneId" = s."ZoneId"
LEFT JOIN device_seat_assignments a
  ON a."SeatId" = s."SeatId"
 AND a."DetachedAtUtc" IS NULL
LEFT JOIN devices d ON d."DeviceId" = a."DeviceId"
WHERE s."SeatId" = '$seatId';
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev

@"
SELECT "DisplayName", "Version", "Publisher"
FROM device_installed_apps
WHERE "DeviceId" = '$($enrollment.deviceId)'
ORDER BY "DisplayName";
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Expected:

```text
Main Hall | PC-SMOKE-001 | ... | PC-SMOKE-001 | t | t
Counter-Strike 2 | 2.0.0    | Valve
Discord          | 1.0.9059 | Discord Inc.
```

Optionally inspect credential revocation state:

```powershell
@"
SELECT "CredentialId", "RevokedAtUtc"
FROM device_credentials
WHERE "DeviceId" = '$($enrollment.deviceId)'
ORDER BY "CreatedAtUtc";
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

## Stop Local Services

Stop the API process with `Ctrl+C`.

Stop PostgreSQL while keeping data:

```powershell
docker compose down
```

Delete local PostgreSQL data:

```powershell
docker compose down -v
```
