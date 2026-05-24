<#
.SYNOPSIS
    Authenticode-sign AFK4 client MSI artifacts.

.DESCRIPTION
    Supports two signing modes:

    1. signtool (default) - signs MSI artifacts in place with either a local
       PFX file (-CertificatePath) or a certificate already imported into a
       Windows certificate store (-CertificateSha1). Requires signtool.exe
       from the Windows SDK.

    2. SignPath cloud signing (-UseSignPath) - submits MSI artifacts to
       https://signpath.io for cloud-managed Authenticode signing. The
       SignPath project, signing policy, and CI user token are passed in via
       -SignPathOrganizationId / -SignPathProjectSlug /
       -SignPathSigningPolicySlug / -SignPathApiTokenEnvVar. Signed MSI files
       replace the originals on disk. Requires the SignPath PowerShell module
       (auto-installed for the current user when missing).
#>
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

    [string] $SigntoolPath,

    [switch] $UseSignPath,

    [string] $SignPathOrganizationId,

    [string] $SignPathProjectSlug,

    [string] $SignPathSigningPolicySlug,

    [string] $SignPathArtifactConfigurationSlug,

    [string] $SignPathApiTokenEnvVar,

    [int] $SignPathTimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'

function Resolve-SigntoolExecutable {
    param(
        [string] $ConfiguredPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        if (-not (Test-Path -LiteralPath $ConfiguredPath -PathType Leaf)) {
            throw "signtool executable was not found at '$ConfiguredPath'."
        }

        return (Resolve-Path -LiteralPath $ConfiguredPath).Path
    }

    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $windowsSdkSigntool = Find-WindowsSdkSigntoolExecutable
    if ($null -ne $windowsSdkSigntool) {
        return $windowsSdkSigntool
    }

    throw "signtool.exe was not found. Install the Windows SDK or pass -SigntoolPath."
}

function Find-WindowsSdkSigntoolExecutable {
    $candidateRoots = @()
    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    $programFiles = [Environment]::GetEnvironmentVariable('ProgramFiles')
    $programW6432 = [Environment]::GetEnvironmentVariable('ProgramW6432')

    foreach ($basePath in @($programFilesX86, $programFiles, $programW6432)) {
        if (-not [string]::IsNullOrWhiteSpace($basePath)) {
            $candidateRoots += Join-Path (Join-Path (Join-Path $basePath 'Windows Kits') '10') 'bin'
        }
    }

    $candidates = @()
    foreach ($candidateRoot in @($candidateRoots | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $candidateRoot -PathType Container)) {
            continue
        }

        $versionDirectories = @(Get-ChildItem -LiteralPath $candidateRoot -Directory -ErrorAction SilentlyContinue)
        foreach ($versionDirectory in $versionDirectories) {
            $version = $versionDirectory.Name -as [version]
            if ($null -eq $version) {
                continue
            }

            foreach ($architecture in @('x64', 'x86', 'arm64', 'arm')) {
                $signtoolPath = Join-Path (Join-Path $versionDirectory.FullName $architecture) 'signtool.exe'
                if (Test-Path -LiteralPath $signtoolPath -PathType Leaf) {
                    $architecturePriority = 1
                    if ($architecture -eq 'x64') {
                        $architecturePriority = 0
                    }

                    $candidates += [pscustomobject]@{
                        Path = (Resolve-Path -LiteralPath $signtoolPath).Path
                        Version = $version
                        ArchitecturePriority = $architecturePriority
                    }
                }
            }
        }
    }

    if ($candidates.Count -eq 0) {
        return $null
    }

    $sortProperties = @(
        @{ Expression = { $_.ArchitecturePriority }; Descending = $false }
        @{ Expression = { $_.Version }; Descending = $true }
        @{ Expression = { $_.Path }; Descending = $false }
    )
    $sortedCandidates = @($candidates | Sort-Object $sortProperties)
    return $sortedCandidates[0].Path
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
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "Package '$path' was not found."
            }

            if (-not [string]::Equals([System.IO.Path]::GetExtension($path), '.msi', [StringComparison]::OrdinalIgnoreCase)) {
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

    if (-not (Test-Path -LiteralPath $ConfiguredPackageDirectory -PathType Container)) {
        throw "Package directory '$ConfiguredPackageDirectory' was not found."
    }

    $packages = @(Get-ChildItem -LiteralPath $ConfiguredPackageDirectory -Filter '*.msi' -File | Sort-Object Name)
    if ($packages.Count -eq 0) {
        throw "Package directory '$ConfiguredPackageDirectory' did not contain MSI artifacts."
    }

    return @($packages | ForEach-Object { $_.FullName })
}

function Invoke-SigntoolSigning {
    param(
        [string[]] $Packages
    )

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
        if (-not (Test-Path -LiteralPath $CertificatePath -PathType Leaf)) {
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

    foreach ($package in $Packages) {
        Write-Host "Signing MSI artifact with signtool: $package"
        & $resolvedSigntoolPath @signArgs $package
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed for '$package' with exit code $LASTEXITCODE."
        }
    }
}

function Ensure-SignPathModule {
    $existing = Get-Module -ListAvailable -Name SignPath | Select-Object -First 1
    if ($null -eq $existing) {
        Write-Host "SignPath PowerShell module not found. Installing for current user from PSGallery..."
        Install-Module -Name SignPath -Scope CurrentUser -Force -AllowClobber
    }

    Import-Module -Name SignPath -ErrorAction Stop
}

function Invoke-SignPathSigning {
    param(
        [string[]] $Packages
    )

    foreach ($name in @('SignPathOrganizationId', 'SignPathProjectSlug', 'SignPathSigningPolicySlug', 'SignPathApiTokenEnvVar')) {
        $value = (Get-Variable -Name $name -Scope 1 -ValueOnly)
        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "$name is required when -UseSignPath is specified."
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($CertificatePath) -or -not [string]::IsNullOrWhiteSpace($CertificateSha1)) {
        throw "Do not combine -UseSignPath with signtool parameters (CertificatePath / CertificateSha1)."
    }

    $apiToken = [Environment]::GetEnvironmentVariable($SignPathApiTokenEnvVar)
    if ([string]::IsNullOrWhiteSpace($apiToken)) {
        throw "Environment variable '$SignPathApiTokenEnvVar' must contain the SignPath CI user token."
    }

    Ensure-SignPathModule

    foreach ($package in $Packages) {
        $signedPath = "$package.signpath-signed"
        Write-Host "Submitting MSI artifact to SignPath: $package"

        $submitParams = @{
            ApiToken                          = $apiToken
            OrganizationId                    = $SignPathOrganizationId
            ProjectSlug                       = $SignPathProjectSlug
            SigningPolicySlug                 = $SignPathSigningPolicySlug
            InputArtifactPath                 = $package
            OutputArtifactPath                = $signedPath
            Force                             = $true
            WaitForCompletion                 = $true
            WaitForCompletionTimeoutInSeconds = $SignPathTimeoutSeconds
        }

        if (-not [string]::IsNullOrWhiteSpace($SignPathArtifactConfigurationSlug)) {
            $submitParams.ArtifactConfigurationSlug = $SignPathArtifactConfigurationSlug
        }

        Submit-SigningRequest @submitParams

        if (-not (Test-Path -LiteralPath $signedPath -PathType Leaf)) {
            throw "SignPath did not produce a signed artifact at '$signedPath'."
        }

        Move-Item -LiteralPath $signedPath -Destination $package -Force
        Write-Host "Replaced unsigned artifact with SignPath-signed version: $package"
    }
}

$packages = Resolve-PackageFiles $PackagePath $PackageDirectory

if ($UseSignPath) {
    Invoke-SignPathSigning -Packages $packages
}
else {
    Invoke-SigntoolSigning -Packages $packages
}
