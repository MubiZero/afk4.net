param(
    [Parameter(Mandatory = $true)]
    [uri] $PlatformBaseUrl,

    [Parameter(Mandatory = $true)]
    [guid] $BranchId,

    [string[]] $RequestPath,

    [string] $RequestDirectory,

    [string] $AccessToken,

    [string] $AccessTokenEnvVar
)

$ErrorActionPreference = 'Stop'

function Resolve-RequestFiles {
    param(
        [string[]] $ExplicitRequestPaths,
        [string] $DirectoryPath
    )

    $explicitPaths = @()
    if ($null -ne $ExplicitRequestPaths) {
        $explicitPaths = @($ExplicitRequestPaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }

    if ($explicitPaths.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($DirectoryPath)) {
        throw "Specify RequestPath or RequestDirectory, not both."
    }

    if ($explicitPaths.Count -gt 0) {
        $resolved = @()
        foreach ($path in $explicitPaths) {
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "RequestPath '$path' was not found."
            }

            if ([System.IO.Path]::GetFileName($path) -notlike '*-request.json') {
                throw "RequestPath must reference *-request.json files."
            }

            $resolved += (Resolve-Path -LiteralPath $path).Path
        }

        return $resolved
    }

    if ([string]::IsNullOrWhiteSpace($DirectoryPath)) {
        throw "Specify RequestPath or RequestDirectory."
    }

    if (-not (Test-Path -LiteralPath $DirectoryPath -PathType Container)) {
        throw "RequestDirectory '$DirectoryPath' was not found."
    }

    $files = @(Get-ChildItem -LiteralPath $DirectoryPath -Filter '*-request.json' -File | Sort-Object Name)
    if ($files.Count -eq 0) {
        throw "RequestDirectory '$DirectoryPath' did not contain *-request.json files."
    }

    return @($files | ForEach-Object { $_.FullName })
}

if ($null -eq $PlatformBaseUrl -or -not $PlatformBaseUrl.IsAbsoluteUri) {
    throw "PlatformBaseUrl must be an absolute URI."
}

if ($PlatformBaseUrl.Scheme -ne 'http' -and $PlatformBaseUrl.Scheme -ne 'https') {
    throw "PlatformBaseUrl must use http or https scheme."
}

$hasDirectToken = -not [string]::IsNullOrWhiteSpace($AccessToken)
$hasTokenEnvVar = -not [string]::IsNullOrWhiteSpace($AccessTokenEnvVar)
if ($hasDirectToken -eq $hasTokenEnvVar) {
    throw "Specify exactly one access token source: AccessToken or AccessTokenEnvVar."
}

if ($hasTokenEnvVar) {
    $AccessToken = [Environment]::GetEnvironmentVariable($AccessTokenEnvVar)
    if ([string]::IsNullOrWhiteSpace($AccessToken)) {
        throw "Environment variable '$AccessTokenEnvVar' must contain the update package registration access token."
    }
}

$requestFiles = Resolve-RequestFiles $RequestPath $RequestDirectory
$baseUri = $PlatformBaseUrl.AbsoluteUri.TrimEnd('/')
$registrationUri = "$baseUri/api/branches/$($BranchId.ToString('D'))/updates/packages"
$headers = @{ Authorization = "Bearer $AccessToken" }

foreach ($requestFile in $requestFiles) {
    $body = Get-Content -LiteralPath $requestFile -Raw
    Invoke-RestMethod -Method Post -Uri $registrationUri -Headers $headers -ContentType 'application/json' -Body $body | Out-Null
    Write-Host "Registered update package request: $requestFile"
}
