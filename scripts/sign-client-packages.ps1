param(
    [string[]] $PackagePath,

    [string] $PackageDirectory,

    [string] $CertificatePath,

    [string] $CertificatePasswordEnvVar,

    [string] $CertificateSha1,

    [ValidateSet('CurrentUser', 'LocalMachine')]
    [string] $CertificateStoreLocation = 'CurrentUser',

    [string] $CertificateStoreName = 'My',

    [string] $TimestampUrl = 'http://timestamp.digicert.com',

    [string] $SigntoolPath
)

$ErrorActionPreference = 'Stop'

function Resolve-SigntoolExecutable {
    param(
        [string] $ConfiguredPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        if (-not (Test-Path -LiteralPath $ConfiguredPath)) {
            throw "signtool executable was not found at '$ConfiguredPath'."
        }

        return (Resolve-Path -LiteralPath $ConfiguredPath).Path
    }

    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    throw "signtool.exe was not found. Install the Windows SDK or pass -SigntoolPath."
}

function Resolve-PackageFiles {
    param(
        [string[]] $ExplicitPackagePaths,
        [string] $ConfiguredPackageDirectory
    )

    $explicitPaths = @()
    if ($null -ne $ExplicitPackagePaths) {
        $explicitPaths = @($ExplicitPackagePaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }

    if ($explicitPaths.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($ConfiguredPackageDirectory)) {
        throw "Specify PackagePath or PackageDirectory, not both."
    }

    if ($explicitPaths.Count -gt 0) {
        $resolved = @()
        foreach ($path in $explicitPaths) {
            if (-not (Test-Path -LiteralPath $path)) {
                throw "Package '$path' was not found."
            }

            if ([System.IO.Path]::GetExtension($path) -ne '.msi') {
                throw "Package '$path' must be an .msi file."
            }

            $resolved += (Resolve-Path -LiteralPath $path).Path
        }

        return $resolved
    }

    $repoRoot = Split-Path -Parent $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($ConfiguredPackageDirectory)) {
        $ConfiguredPackageDirectory = Join-Path $repoRoot 'artifacts/client-packages'
    }

    if (-not (Test-Path -LiteralPath $ConfiguredPackageDirectory)) {
        throw "Package directory '$ConfiguredPackageDirectory' was not found."
    }

    $packages = @(Get-ChildItem -LiteralPath $ConfiguredPackageDirectory -Filter '*.msi' -File | Sort-Object Name)
    if ($packages.Count -eq 0) {
        throw "Package directory '$ConfiguredPackageDirectory' did not contain MSI artifacts."
    }

    return @($packages | ForEach-Object { $_.FullName })
}

$usingPfx = -not [string]::IsNullOrWhiteSpace($CertificatePath)
$usingCertificateStore = -not [string]::IsNullOrWhiteSpace($CertificateSha1)

if ($usingPfx -eq $usingCertificateStore) {
    throw "Specify exactly one Authenticode signing source: CertificatePath or CertificateSha1."
}

if ([string]::IsNullOrWhiteSpace($TimestampUrl)) {
    throw "TimestampUrl is required."
}

$signArgs = @('sign', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256')

if ($usingPfx) {
    if (-not (Test-Path -LiteralPath $CertificatePath)) {
        throw "CertificatePath '$CertificatePath' was not found."
    }

    if ([string]::IsNullOrWhiteSpace($CertificatePasswordEnvVar)) {
        throw "CertificatePasswordEnvVar is required when CertificatePath is supplied."
    }

    $certificatePassword = [Environment]::GetEnvironmentVariable($CertificatePasswordEnvVar)
    if ([string]::IsNullOrWhiteSpace($certificatePassword)) {
        throw "Environment variable '$CertificatePasswordEnvVar' must contain the PFX password."
    }

    $signArgs += @('/f', (Resolve-Path -LiteralPath $CertificatePath).Path, '/p', $certificatePassword)
}
else {
    $signArgs += @('/sha1', $CertificateSha1, '/s', $CertificateStoreName)
    if ($CertificateStoreLocation -eq 'LocalMachine') {
        $signArgs += '/sm'
    }
}

$resolvedSigntoolPath = Resolve-SigntoolExecutable $SigntoolPath
$packages = Resolve-PackageFiles $PackagePath $PackageDirectory

foreach ($package in $packages) {
    Write-Host "Signing MSI artifact: $package"
    & $resolvedSigntoolPath @signArgs $package
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for '$package' with exit code $LASTEXITCODE."
    }
}
