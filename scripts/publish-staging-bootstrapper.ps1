param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [ValidateSet('internal', 'beta', 'stable')]
    [string] $Channel = 'internal',

    [string] $PackageDirectory = '',

    [string] $OutputDirectory = '',

    [Parameter(Mandatory = $true)]
    [uri] $S3Endpoint,

    [Parameter(Mandatory = $true)]
    [string] $S3Bucket,

    [string] $S3KeyPrefix = 'bootstrap/gaming-pc',

    [Parameter(Mandatory = $true)]
    [string] $S3PublicBaseUri,

    [Parameter(Mandatory = $true)]
    [string] $S3AccessKeyEnvVar,

    [Parameter(Mandatory = $true)]
    [string] $S3SecretKeyEnvVar,

    [string] $S3Region = 'us-east-1',

    [uri] $PlatformBaseUrl = 'https://afk4.staging.mubi.dev',

    [guid] $OrganizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',

    [guid] $BranchId = 'acfc0212-967f-4d84-94be-9003387b09c2',

    [guid] $SmokeSeatId = '9f3adbd3-957e-4dc8-8d34-a6bfa56b9275',

    [string] $LeaseSigningPublicKeyPath = '',

    [string] $UpdateSigningPublicKeyPath = ''
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Net.Http

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $PackageDirectory = Join-Path $repoRoot 'artifacts/client-packages'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts/bootstrapper'
}

if ([string]::IsNullOrWhiteSpace($LeaseSigningPublicKeyPath)) {
    $LeaseSigningPublicKeyPath = Join-Path $repoRoot 'deploy/coolify/staging-session-signing-public.pem'
}

if ([string]::IsNullOrWhiteSpace($UpdateSigningPublicKeyPath)) {
    $UpdateSigningPublicKeyPath = Join-Path $repoRoot 'deploy/coolify/staging-update-signing-public.pem'
}

function ConvertTo-Hex {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]] $Bytes
    )

    return -join ($Bytes | ForEach-Object { $_.ToString('x2') })
}

function Get-HmacSha256Bytes {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]] $Key,

        [Parameter(Mandatory = $true)]
        [string] $Data
    )

    $hmac = New-Object System.Security.Cryptography.HMACSHA256 -ArgumentList @(,$Key)
    try {
        return $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Data))
    }
    finally {
        $hmac.Dispose()
    }
}

function Get-Sha256HexForText {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ConvertTo-Hex -Bytes ($sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value)))
    }
    finally {
        $sha256.Dispose()
    }
}

function Join-S3Key {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Segments
    )

    $clean = New-Object System.Collections.Generic.List[string]
    foreach ($rawSegment in $Segments) {
        if ([string]::IsNullOrWhiteSpace($rawSegment)) {
            continue
        }

        foreach ($segment in ([string]$rawSegment -split '\s+')) {
            foreach ($part in $segment -split '[\\/]+') {
                if (-not [string]::IsNullOrWhiteSpace($part)) {
                    $clean.Add($part.Trim())
                }
            }
        }
    }

    return [string]::Join('/', $clean)
}

function New-S3ObjectUri {
    param(
        [Parameter(Mandatory = $true)]
        [uri] $Endpoint,

        [Parameter(Mandatory = $true)]
        [string] $Bucket,

        [Parameter(Mandatory = $true)]
        [string] $ObjectKey
    )

    $baseUri = $Endpoint.AbsoluteUri.TrimEnd('/') + '/'
    $pathSegments = @($Bucket) + ($ObjectKey -split '/')
    $escapedPath = ($pathSegments | ForEach-Object {
        [System.Uri]::EscapeDataString($_)
    }) -join '/'

    return [uri]($baseUri + $escapedPath)
}

function New-PublicUri {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PublicBaseUri,

        [Parameter(Mandatory = $true)]
        [string] $ObjectKey
    )

    $baseUri = $PublicBaseUri.TrimEnd('/') + '/'
    $relativePath = (($ObjectKey -split '/') | ForEach-Object {
        [System.Uri]::EscapeDataString($_)
    }) -join '/'

    return $baseUri + $relativePath
}

function ConvertTo-PowerShellSingleQuotedLiteral {
    param(
        [AllowNull()]
        [string] $Value
    )

    if ($null -eq $Value) {
        return "''"
    }

    return "'" + $Value.Replace("'", "''") + "'"
}

function Invoke-S3PutObject {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $ObjectKey,

        [Parameter(Mandatory = $true)]
        [string] $ContentType
    )

    $accessKey = [Environment]::GetEnvironmentVariable($S3AccessKeyEnvVar)
    if ([string]::IsNullOrWhiteSpace($accessKey)) {
        throw "S3 access key environment variable '$S3AccessKeyEnvVar' is missing or empty."
    }

    $secretKey = [Environment]::GetEnvironmentVariable($S3SecretKeyEnvVar)
    if ([string]::IsNullOrWhiteSpace($secretKey)) {
        throw "S3 secret key environment variable '$S3SecretKeyEnvVar' is missing or empty."
    }

    $objectUri = New-S3ObjectUri -Endpoint $S3Endpoint -Bucket $S3Bucket -ObjectKey $ObjectKey
    $payloadHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
    $amzDate = (Get-Date).ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'")
    $dateStamp = $amzDate.Substring(0, 8)
    $canonicalUri = $objectUri.AbsolutePath
    $canonicalHeaders = "host:$($objectUri.Authority)`nx-amz-content-sha256:$payloadHash`nx-amz-date:$amzDate`n"
    $signedHeaders = 'host;x-amz-content-sha256;x-amz-date'
    $canonicalRequest = "PUT`n$canonicalUri`n`n$canonicalHeaders`n$signedHeaders`n$payloadHash"
    $canonicalRequestHash = Get-Sha256HexForText -Value $canonicalRequest
    $credentialScope = "$dateStamp/$S3Region/s3/aws4_request"
    $stringToSign = "AWS4-HMAC-SHA256`n$amzDate`n$credentialScope`n$canonicalRequestHash"

    $dateKey = Get-HmacSha256Bytes -Key ([System.Text.Encoding]::UTF8.GetBytes("AWS4$secretKey")) -Data $dateStamp
    $dateRegionKey = Get-HmacSha256Bytes -Key $dateKey -Data $S3Region
    $dateRegionServiceKey = Get-HmacSha256Bytes -Key $dateRegionKey -Data 's3'
    $signingKey = Get-HmacSha256Bytes -Key $dateRegionServiceKey -Data 'aws4_request'
    $signature = ConvertTo-Hex -Bytes (Get-HmacSha256Bytes -Key $signingKey -Data $stringToSign)
    $authorization = "AWS4-HMAC-SHA256 Credential=$accessKey/$credentialScope, SignedHeaders=$signedHeaders, Signature=$signature"

    $client = [System.Net.Http.HttpClient]::new()
    $stream = [System.IO.File]::OpenRead($Path)
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Put, $objectUri)
    try {
        $request.Headers.Host = $objectUri.Authority
        $request.Headers.TryAddWithoutValidation('x-amz-content-sha256', $payloadHash) | Out-Null
        $request.Headers.TryAddWithoutValidation('x-amz-date', $amzDate) | Out-Null
        $request.Headers.TryAddWithoutValidation('Authorization', $authorization) | Out-Null
        $request.Content = [System.Net.Http.StreamContent]::new($stream)
        $request.Content.Headers.ContentLength = (Get-Item -LiteralPath $Path).Length
        $request.Content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse($ContentType)

        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            throw "S3 PUT failed for '$ObjectKey' with HTTP $([int]$response.StatusCode): $body"
        }
    }
    finally {
        if ($null -ne $request) {
            $request.Dispose()
        }

        $stream.Dispose()
        $client.Dispose()
    }
}

$msiFileName = "afk4-gaming-pc-$Version-$Channel.msi"
$msiPath = Join-Path $PackageDirectory $msiFileName
if (-not (Test-Path -LiteralPath $msiPath)) {
    throw "Staging Gaming PC MSI was not found: $msiPath"
}

if (-not (Test-Path -LiteralPath $LeaseSigningPublicKeyPath)) {
    throw "Lease signing public key was not found: $LeaseSigningPublicKeyPath"
}

if (-not (Test-Path -LiteralPath $UpdateSigningPublicKeyPath)) {
    throw "Update signing public key was not found: $UpdateSigningPublicKeyPath"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$msiObjectKey = Join-S3Key -Segments @('agent-service', $Channel, $Version, $msiFileName)
$msiArtifactUri = New-PublicUri -PublicBaseUri $S3PublicBaseUri -ObjectKey $msiObjectKey
$msiSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $msiPath).Hash.ToLowerInvariant()
$msiSizeBytes = (Get-Item -LiteralPath $msiPath).Length
$leaseSigningPublicKeyPem = Get-Content -LiteralPath $LeaseSigningPublicKeyPath -Raw
$updateSigningPublicKeyPem = Get-Content -LiteralPath $UpdateSigningPublicKeyPath -Raw

$bootstrapFileName = "install-afk4-gaming-pc-$Version-$Channel.ps1"
$bootstrapObjectKey = Join-S3Key -Segments @($S3KeyPrefix, $Channel, $Version, $bootstrapFileName)
$bootstrapUri = New-PublicUri -PublicBaseUri $S3PublicBaseUri -ObjectKey $bootstrapObjectKey

$bootstrapTemplate = @'
param(
    [string] $UserName = 'real-device-smoke@afk4.test',
    [securestring] $Password,
    [string] $MachineName = [Environment]::MachineName
)

$ErrorActionPreference = 'Stop'

$platformBaseUrl = @@PLATFORM_BASE_URL@@
$organizationId = @@ORGANIZATION_ID@@
$branchId = @@BRANCH_ID@@
$smokeSeatId = @@SMOKE_SEAT_ID@@
$version = @@VERSION@@
$channel = @@CHANNEL@@
$msiUri = @@MSI_URI@@
$msiSha256 = @@MSI_SHA256@@
$leaseSigningPublicKeyPem = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(@@LEASE_SIGNING_PUBLIC_KEY_PEM_BASE64@@))
$updateSigningPublicKeyPem = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(@@UPDATE_SIGNING_PUBLIC_KEY_PEM_BASE64@@))

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this bootstrapper from an elevated Administrator PowerShell session.'
    }
}

function Convert-SecureStringToPlainText {
    param(
        [Parameter(Mandatory = $true)]
        [securestring] $Value
    )

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Invoke-Afk4Api {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Method,

        [Parameter(Mandatory = $true)]
        [string] $Path,

        [object] $Body = $null,

        [string] $AccessToken = ''
    )

    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($AccessToken)) {
        $headers['Authorization'] = "Bearer $AccessToken"
    }

    $uri = "$($platformBaseUrl.TrimEnd('/'))/$($Path.TrimStart('/'))"
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
    }

    return Invoke-RestMethod `
        -Method $Method `
        -Uri $uri `
        -Headers $headers `
        -ContentType 'application/json' `
        -Body ($Body | ConvertTo-Json -Depth 10)
}

function Set-MachineEnvironmentVariable {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    [Environment]::SetEnvironmentVariable($Name, $Value, [EnvironmentVariableTarget]::Machine)
}

Assert-Administrator

if ($null -eq $Password) {
    $credential = Get-Credential -UserName $UserName -Message 'AFK4 staging staff credentials'
    $UserName = $credential.UserName
    $passwordText = Convert-SecureStringToPlainText -Value $credential.Password
}
else {
    $passwordText = Convert-SecureStringToPlainText -Value $Password
}

$workRoot = 'C:\AFK4-Smoke'
$packageRoot = Join-Path $workRoot 'Packages'
$logRoot = 'C:\ProgramData\AFK4\Agent\InstallLogs'
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

$msiPath = Join-Path $packageRoot "afk4-gaming-pc-$version-$channel.msi"
curl.exe -L --fail --retry 5 --retry-delay 5 -o $msiPath $msiUri
if ($LASTEXITCODE -ne 0) {
    throw "curl.exe failed to download $msiUri with exit code $LASTEXITCODE."
}

$actualMsiSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $msiPath).Hash.ToLowerInvariant()
if ($actualMsiSha256 -ne $msiSha256) {
    throw "SHA mismatch for Gaming PC MSI. Expected $msiSha256, got $actualMsiSha256."
}

Invoke-Afk4Api -Method Get -Path 'api/health' | Out-Null

$signIn = Invoke-Afk4Api -Method Post -Path 'api/auth/staff/sign-in' -Body @{
    organizationId = $organizationId
    userName = $UserName
    password = $passwordText
}

if ([string]::IsNullOrWhiteSpace($signIn.accessToken)) {
    throw 'Staging sign-in response did not include an access token.'
}

$accessToken = [string]$signIn.accessToken
$enrollmentCode = Invoke-Afk4Api `
    -Method Post `
    -Path "api/branches/$branchId/device-enrollment-codes" `
    -AccessToken $accessToken `
    -Body @{
        organizationId = $organizationId
        expiresInSeconds = 300
    }

if ([string]::IsNullOrWhiteSpace($enrollmentCode.code)) {
    throw 'Enrollment code response did not include a code.'
}

$enrollment = Invoke-Afk4Api -Method Post -Path 'api/devices/enroll' -Body @{
    organizationId = $organizationId
    branchId = $branchId
    enrollmentCode = [string]$enrollmentCode.code
    machineName = $MachineName
    agentVersion = $version
    shellVersion = $version
    observedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
}

if ([string]::IsNullOrWhiteSpace($enrollment.deviceId) -or [string]::IsNullOrWhiteSpace($enrollment.credentialSecret)) {
    throw 'Enrollment response did not include deviceId and credentialSecret.'
}

$assignment = Invoke-Afk4Api `
    -Method Post `
    -Path "api/branches/$branchId/devices/$($enrollment.deviceId)/seat-assignment" `
    -AccessToken $accessToken `
    -Body @{
        organizationId = $organizationId
        seatId = $smokeSeatId
    }

Write-Host "Device enrolled: $($enrollment.deviceId)"
Write-Host "Seat assigned: $($assignment.seatId)"

Stop-Service AFK4.Agent.Service -Force -ErrorAction SilentlyContinue
$installLog = Join-Path $logRoot 'gaming-pc-bootstrapper-install.log'
$msiexecPath = Join-Path $env:WINDIR 'System32\msiexec.exe'
$msiArguments = "/i `"$msiPath`" /qn /norestart /L*v `"$installLog`""
$msiProcess = Start-Process -FilePath $msiexecPath -ArgumentList $msiArguments -Wait -PassThru
if ($msiProcess.ExitCode -ne 0) {
    throw "msiexec.exe exited with code $($msiProcess.ExitCode). Log: $installLog"
}

Stop-Service AFK4.Agent.Service -Force -ErrorAction SilentlyContinue

Set-MachineEnvironmentVariable -Name 'Agent__PlatformBaseUrl' -Value $platformBaseUrl.TrimEnd('/')
Set-MachineEnvironmentVariable -Name 'Agent__OrganizationId' -Value $organizationId
Set-MachineEnvironmentVariable -Name 'Agent__BranchId' -Value $branchId
Set-MachineEnvironmentVariable -Name 'Agent__DeviceId' -Value ([string]$enrollment.deviceId)
Set-MachineEnvironmentVariable -Name 'Agent__MachineName' -Value $MachineName
Set-MachineEnvironmentVariable -Name 'Agent__AgentVersion' -Value $version
Set-MachineEnvironmentVariable -Name 'Agent__ShellVersion' -Value $version
Set-MachineEnvironmentVariable -Name 'Agent__DeviceCredentialSecret' -Value ([string]$enrollment.credentialSecret)
Set-MachineEnvironmentVariable -Name 'Agent__LeaseSigningPublicKeyPem' -Value $leaseSigningPublicKeyPem
Set-MachineEnvironmentVariable -Name 'Agent__PlayerShellExecutablePath' -Value 'C:\Program Files\AFK4\Player Shell\AFK4.Player.Shell.exe'
Set-MachineEnvironmentVariable -Name 'Agent__PlayerShellAutoStartEnabled' -Value ([bool]::TrueString)
Set-MachineEnvironmentVariable -Name 'Agent__UpdateChannel' -Value $channel
Set-MachineEnvironmentVariable -Name 'Agent__UpdateInstallerExecutablePath' -Value 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
Set-MachineEnvironmentVariable -Name 'Agent__UpdateInstallerArgumentsTemplate' -Value '-NoProfile -ExecutionPolicy Bypass -File "C:\Program Files\AFK4\Update Helpers\install-afk4-update-msi.ps1" -PackagePath "{PackagePath}" -Component "{Component}" -Version "{Version}"'
Set-MachineEnvironmentVariable -Name 'Agent__UpdateRollbackExecutablePath' -Value 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
Set-MachineEnvironmentVariable -Name 'Agent__UpdateRollbackArgumentsTemplate' -Value '-NoProfile -ExecutionPolicy Bypass -File "C:\Program Files\AFK4\Update Helpers\rollback-afk4-update-msi.ps1" -PackagePath "{PackagePath}" -Component "{Component}" -Version "{Version}"'
Set-MachineEnvironmentVariable -Name 'Agent__UpdateRestartExecutablePath' -Value 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
Set-MachineEnvironmentVariable -Name 'Agent__UpdateRestartArgumentsTemplate' -Value '-NoProfile -ExecutionPolicy Bypass -File "C:\Program Files\AFK4\Update Helpers\restart-afk4-agent-service.ps1"'
Set-MachineEnvironmentVariable -Name 'Agent__UpdatePackageSigningPublicKeyPem' -Value $updateSigningPublicKeyPem

Start-Service AFK4.Agent.Service

$deadline = (Get-Date).ToUniversalTime().AddSeconds(75)
do {
    Start-Sleep -Seconds 3
    $detail = Invoke-Afk4Api -Method Get -Path "api/devices/$($enrollment.deviceId)" -AccessToken $accessToken
    if ($detail.isOnline -and $null -ne $detail.lastHeartbeatAtUtc) {
        Write-Host "Backend reports the device online: $($detail.lastHeartbeatAtUtc)"
        Write-Host "AFK4 Gaming PC bootstrap completed. DeviceId: $($enrollment.deviceId)"
        exit 0
    }
} while ((Get-Date).ToUniversalTime() -lt $deadline)

Write-Warning 'Service started, but backend heartbeat was not observed before timeout.'
Write-Host "DeviceId: $($enrollment.deviceId)"
'@

$replacements = @{
    '@@PLATFORM_BASE_URL@@' = ConvertTo-PowerShellSingleQuotedLiteral $PlatformBaseUrl.AbsoluteUri.TrimEnd('/')
    '@@ORGANIZATION_ID@@' = ConvertTo-PowerShellSingleQuotedLiteral $OrganizationId.ToString('D')
    '@@BRANCH_ID@@' = ConvertTo-PowerShellSingleQuotedLiteral $BranchId.ToString('D')
    '@@SMOKE_SEAT_ID@@' = ConvertTo-PowerShellSingleQuotedLiteral $SmokeSeatId.ToString('D')
    '@@VERSION@@' = ConvertTo-PowerShellSingleQuotedLiteral $Version
    '@@CHANNEL@@' = ConvertTo-PowerShellSingleQuotedLiteral $Channel
    '@@MSI_URI@@' = ConvertTo-PowerShellSingleQuotedLiteral $msiArtifactUri
    '@@MSI_SHA256@@' = ConvertTo-PowerShellSingleQuotedLiteral $msiSha256
    '@@LEASE_SIGNING_PUBLIC_KEY_PEM_BASE64@@' = ConvertTo-PowerShellSingleQuotedLiteral ([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($leaseSigningPublicKeyPem)))
    '@@UPDATE_SIGNING_PUBLIC_KEY_PEM_BASE64@@' = ConvertTo-PowerShellSingleQuotedLiteral ([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($updateSigningPublicKeyPem)))
}

$bootstrapScript = $bootstrapTemplate
foreach ($key in $replacements.Keys) {
    $bootstrapScript = $bootstrapScript.Replace($key, [string]$replacements[$key])
}

$bootstrapPath = Join-Path $OutputDirectory $bootstrapFileName
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($bootstrapPath, $bootstrapScript, $utf8NoBom)
$bootstrapSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $bootstrapPath).Hash.ToLowerInvariant()
$bootstrapSizeBytes = (Get-Item -LiteralPath $bootstrapPath).Length
$publishedAtUtc = (Get-Date).ToUniversalTime().ToString('o')

$manifest = [ordered]@{
    component = 'gaming-pc-bootstrap'
    version = $Version
    channel = $Channel
    artifactUri = $bootstrapUri
    sha256 = $bootstrapSha256
    sizeBytes = $bootstrapSizeBytes
    fileName = $bootstrapFileName
    packageUri = $msiArtifactUri
    packageSha256 = $msiSha256
    packageSizeBytes = $msiSizeBytes
    publishedAtUtc = $publishedAtUtc
}

$versionManifestPath = Join-Path $OutputDirectory "afk4-gaming-pc-bootstrap-$Version-$Channel.json"
$latestManifestPath = Join-Path $OutputDirectory "afk4-gaming-pc-bootstrap-$Channel-latest.json"
$manifestJson = $manifest | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($versionManifestPath, $manifestJson, $utf8NoBom)
[System.IO.File]::WriteAllText($latestManifestPath, $manifestJson, $utf8NoBom)

Invoke-S3PutObject -Path $bootstrapPath -ObjectKey $bootstrapObjectKey -ContentType 'text/plain'
Invoke-S3PutObject -Path $versionManifestPath -ObjectKey (Join-S3Key -Segments @($S3KeyPrefix, $Channel, $Version, 'manifest.json')) -ContentType 'application/json'
Invoke-S3PutObject -Path $latestManifestPath -ObjectKey (Join-S3Key -Segments @($S3KeyPrefix, $Channel, 'latest.json')) -ContentType 'application/json'

Write-Host "Published staging Gaming PC bootstrapper:"
Write-Host $bootstrapUri
Write-Host "Latest manifest:"
Write-Host (New-PublicUri -PublicBaseUri $S3PublicBaseUri -ObjectKey (Join-S3Key -Segments @($S3KeyPrefix, $Channel, 'latest.json')))
