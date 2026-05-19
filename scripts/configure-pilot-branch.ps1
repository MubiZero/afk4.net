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
    [long]$ProductPriceMinorUnits = 500
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

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
    $parameters = @{
        Method = $Method
        Uri = $uri
        Headers = $Headers
    }

    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 20 -Compress
    }

    Invoke-RestMethod @parameters
}

$signIn = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/auth/staff/sign-in" `
    -Body @{
        organizationId = $OrganizationId
        userName = $AdminUserName
        password = $AdminPassword
    }

$headers = @{
    Authorization = "Bearer $($signIn.accessToken)"
}

$cashier = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/branches/$BranchId/staff" `
    -Headers $headers `
    -Body @{
        organizationId = $OrganizationId
        userName = $CashierUserName
        displayName = $CashierDisplayName
        password = $CashierPassword
        roleNames = @("cashier_operator")
    }

$technician = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/branches/$BranchId/staff" `
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
    -Path "/api/branches/$BranchId/layout/zones" `
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
        -Path "/api/branches/$BranchId/layout/seats" `
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
    -Path "/api/branches/$BranchId/tariffs" `
    -Headers $headers `
    -Body @{
        organizationId = $OrganizationId
        name = $TariffName
        idempotencyKey = "pilot-setup-$BranchId-tariff-$TariffName"
    }

$tariffVersion = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/branches/$BranchId/tariffs/$($tariff.tariffId)/versions" `
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
    -Path "/api/branches/$BranchId/pos/categories" `
    -Headers $headers `
    -Body @{
        organizationId = $OrganizationId
        name = $ProductCategoryName
        idempotencyKey = "pilot-setup-$BranchId-pos-category-$ProductCategoryName"
    }

$product = Invoke-Afk4Json `
    -Method Post `
    -Path "/api/branches/$BranchId/pos/products" `
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
