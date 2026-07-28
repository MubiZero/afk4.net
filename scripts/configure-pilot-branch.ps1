param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [Parameter(Mandatory = $true)]
    [Guid]$OrganizationId,

    [Parameter(Mandatory = $true)]
    [Guid]$BranchId,

    [Parameter(Mandatory = $true)]
    [string]$AdminUserName,

    [Parameter(Mandatory = $true)]
    [string]$AdminPassword,

    [string]$CashierUserName = "cashier.pilot@afk4.test",

    [string]$CashierDisplayName = "Pilot Cashier",

    [string]$CashierPassword = "ChangeMe!2026",

    [string]$TechnicianUserName = "technician.pilot@afk4.test",

    [string]$TechnicianDisplayName = "Pilot Technician",

    [string]$TechnicianPassword = "ChangeMe!2026",

    [string]$ZoneName = "Main Hall",

    [string]$SeatPrefix = "PC-",

    [ValidateRange(1, 200)]
    [int]$SeatCount = 10,

    [string]$TariffName = "Standard",

    [string]$CurrencyCode = "TJS",

    [ValidateRange(1, [int]::MaxValue)]
    [long]$PricePerMinuteMinorUnits = 100,

    [ValidateRange(1, 1440)]
    [int]$MinimumBillableMinutes = 1,

    [ValidateRange(1, 1440)]
    [int]$RoundingIncrementMinutes = 1,

    [string]$ProductCategoryName = "Drinks",

    [string]$ProductName = "Water 0.5",

    [string]$ProductSku = "WATER-05",

    [ValidateRange(1, [int]::MaxValue)]
    [long]$ProductPriceMinorUnits = 500,

    [ValidateRange(1, 600)]
    [int]$RequestTimeoutSeconds = 60
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

$curlPath = (Get-Command curl.exe -ErrorAction Stop).Source

function Invoke-Afk4Json {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [object]$Body,

        [hashtable]$Headers = @{}
    )

    $uri = "$($BaseUrl.TrimEnd('/'))$Path"
    $requestPath = $null
    $responsePath = Join-Path $env:TEMP ("afk4-response-{0}.json" -f [Guid]::NewGuid().ToString("N"))

    try {
        $curlArguments = @(
            "-sS",
            "--max-time", $RequestTimeoutSeconds.ToString(),
            "-X", $Method.ToUpperInvariant(),
            "-H", "Accept: application/json",
            "-o", $responsePath,
            "-w", "%{http_code}"
        )

        foreach ($header in $Headers.GetEnumerator()) {
            $curlArguments += @("-H", "$($header.Key): $($header.Value)")
        }

        if ($null -ne $Body) {
            $requestPath = Join-Path $env:TEMP ("afk4-request-{0}.json" -f [Guid]::NewGuid().ToString("N"))
            $requestJson = $Body | ConvertTo-Json -Depth 20 -Compress
            [System.IO.File]::WriteAllText(
                $requestPath,
                $requestJson,
                [System.Text.UTF8Encoding]::new($false))

            $curlArguments += @(
                "-H", "Content-Type: application/json",
                "--data-binary", "@$requestPath"
            )
        }

        $curlArguments += $uri
        $statusText = & $curlPath @curlArguments
        $exitCode = $LASTEXITCODE
        $responseText = if (Test-Path -LiteralPath $responsePath) {
            [System.IO.File]::ReadAllText($responsePath)
        }
        else {
            ""
        }

        if ($exitCode -ne 0) {
            throw "AFK4 API request failed: curl exited with code $exitCode for $Method $Path."
        }

        $statusCode = [int]$statusText
        if ($statusCode -lt 200 -or $statusCode -gt 299) {
            throw "AFK4 API request failed: $Method $Path returned HTTP $statusCode. Body: $responseText"
        }

        if ([string]::IsNullOrWhiteSpace($responseText)) {
            return $null
        }

        return $responseText | ConvertFrom-Json
    }
    finally {
        if ($null -ne $requestPath) {
            Remove-Item -LiteralPath $requestPath -Force -ErrorAction SilentlyContinue
        }

        Remove-Item -LiteralPath $responsePath -Force -ErrorAction SilentlyContinue
    }
}

$clientHeaders = @{
    'X-AFK4-Product' = 'organization-admin'
    'X-AFK4-Compatibility-Epoch' = '2'
    'X-AFK4-Client-Version' = '0.2.0-pilot-setup'
}

$signIn = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/organizations/$OrganizationId/auth/staff/sign-in" `
    -Headers $clientHeaders `
    -Body @{
        organizationId = $OrganizationId
        userName = $AdminUserName
        password = $AdminPassword
    }

$headers = @{
    Authorization = "Bearer $($signIn.accessToken)"
    'X-AFK4-Product' = 'organization-admin'
    'X-AFK4-Compatibility-Epoch' = '2'
    'X-AFK4-Client-Version' = '0.2.0-pilot-setup'
}

$cashier = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/organizations/$OrganizationId/branches/$BranchId/staff" `
    -Headers $headers `
    -Body @{
        organizationId = $OrganizationId
        userName = $CashierUserName
        displayName = $CashierDisplayName
        password = $CashierPassword
        roleNames = @("operator")
    }

$technician = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/organizations/$OrganizationId/branches/$BranchId/staff" `
    -Headers $headers `
    -Body @{
        organizationId = $OrganizationId
        userName = $TechnicianUserName
        displayName = $TechnicianDisplayName
        password = $TechnicianPassword
        roleNames = @("technician")
    }

$zone = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/organizations/$OrganizationId/branches/$BranchId/layout/zones" `
    -Headers $headers `
    -Body @{
        organizationId = $OrganizationId
        name = $ZoneName
        sortOrder = 10
    }

$seats = @()
for ($index = 1; $index -le $SeatCount; $index++) {
    $seatName = "{0}{1:000}" -f $SeatPrefix, $index
    $seats += Invoke-Afk4Json `
        -Method Post `
        -Path "/api/organizations/$OrganizationId/branches/$BranchId/layout/seats" `
        -Headers $headers `
        -Body @{
            organizationId = $OrganizationId
            zoneId = $zone.zoneId
            name = $seatName
            sortOrder = $index
        }
}

$tariff = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/organizations/$OrganizationId/branches/$BranchId/tariffs" `
    -Headers $headers `
    -Body @{
        organizationId = $OrganizationId
        name = $TariffName
        idempotencyKey = "pilot-setup-$BranchId-tariff-$TariffName"
    }

$tariffVersion = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/organizations/$OrganizationId/branches/$BranchId/tariffs/$($tariff.tariffId)/versions" `
    -Headers $headers `
    -Body @{
        organizationId = $OrganizationId
        tariffId = $tariff.tariffId
        currencyCode = $CurrencyCode
        pricePerMinuteMinorUnits = $PricePerMinuteMinorUnits
        minimumBillableMinutes = $MinimumBillableMinutes
        roundingIncrementMinutes = $RoundingIncrementMinutes
        effectiveFromUtc = (Get-Date).ToUniversalTime().ToString("o")
        idempotencyKey = "pilot-setup-$BranchId-tariff-version-$TariffName"
    }

$category = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/organizations/$OrganizationId/branches/$BranchId/pos/categories" `
    -Headers $headers `
    -Body @{
        organizationId = $OrganizationId
        name = $ProductCategoryName
        idempotencyKey = "pilot-setup-$BranchId-pos-category-$ProductCategoryName"
    }

$product = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/organizations/$OrganizationId/branches/$BranchId/pos/products" `
    -Headers $headers `
    -Body @{
        organizationId = $OrganizationId
        categoryId = $category.categoryId
        name = $ProductName
        sku = $ProductSku
        price = @{
            currencyCode = $CurrencyCode
            minorUnits = $ProductPriceMinorUnits
        }
        trackStock = $true
        allowNegativeStock = $false
        idempotencyKey = "pilot-setup-$BranchId-pos-product-$ProductSku"
    }

[pscustomobject]@{
    BaseUrl = $BaseUrl
    OrganizationId = $OrganizationId
    BranchId = $BranchId
    CashierUserId = $cashier.staffUserId
    TechnicianUserId = $technician.staffUserId
    ZoneId = $zone.zoneId
    SeatCount = $seats.Count
    TariffId = $tariff.tariffId
    TariffVersionId = $tariffVersion.tariffVersionId
    ProductCategoryId = $category.categoryId
    ProductId = $product.productId
} | ConvertTo-Json -Depth 10
