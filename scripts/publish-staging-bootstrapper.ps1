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

    [string] $S3Region = 'us-east-1'
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
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]] $Segments
    )

    $clean = New-Object System.Collections.Generic.List[string]
    foreach ($segment in $Segments) {
        if ([string]::IsNullOrWhiteSpace($segment)) {
            continue
        }

        foreach ($part in $segment -split '[\\/]+') {
            if (-not [string]::IsNullOrWhiteSpace($part)) {
                $clean.Add($part.Trim())
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
    $escapedPath = (($Bucket, ($ObjectKey -split '/')) | ForEach-Object {
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

$setupFileName = "afk4-gaming-pc-setup-$Version-$Channel.exe"
$setupPath = Join-Path $PackageDirectory $setupFileName
if (-not (Test-Path -LiteralPath $setupPath)) {
    throw "Staging Gaming PC setup executable was not found: $setupPath"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$versionedObjectKey = Join-S3Key $S3KeyPrefix $Channel $Version $setupFileName
$artifactUri = New-PublicUri -PublicBaseUri $S3PublicBaseUri -ObjectKey $versionedObjectKey
$sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $setupPath).Hash.ToLowerInvariant()
$sizeBytes = (Get-Item -LiteralPath $setupPath).Length
$publishedAtUtc = (Get-Date).ToUniversalTime().ToString('o')

$manifest = [ordered]@{
    component = 'gaming-pc-setup'
    version = $Version
    channel = $Channel
    artifactUri = $artifactUri
    sha256 = $sha256
    sizeBytes = $sizeBytes
    fileName = $setupFileName
    publishedAtUtc = $publishedAtUtc
}

$versionManifestPath = Join-Path $OutputDirectory "afk4-gaming-pc-setup-$Version-$Channel.json"
$latestManifestPath = Join-Path $OutputDirectory "afk4-gaming-pc-setup-$Channel-latest.json"
$manifestJson = $manifest | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($versionManifestPath, $manifestJson, [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText($latestManifestPath, $manifestJson, [System.Text.UTF8Encoding]::new($false))

Invoke-S3PutObject -Path $setupPath -ObjectKey $versionedObjectKey -ContentType 'application/vnd.microsoft.portable-executable'
Invoke-S3PutObject -Path $versionManifestPath -ObjectKey (Join-S3Key $S3KeyPrefix $Channel $Version 'manifest.json') -ContentType 'application/json'
Invoke-S3PutObject -Path $latestManifestPath -ObjectKey (Join-S3Key $S3KeyPrefix $Channel 'latest.json') -ContentType 'application/json'

Write-Host "Published staging Gaming PC setup:"
Write-Host $artifactUri
Write-Host "Latest manifest:"
Write-Host (New-PublicUri -PublicBaseUri $S3PublicBaseUri -ObjectKey (Join-S3Key $S3KeyPrefix $Channel 'latest.json'))
