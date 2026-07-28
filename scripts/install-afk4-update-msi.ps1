param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [Parameter(Mandatory = $true)]
    [string] $Component,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $LogDirectory = "$env:ProgramData\AFK4\Agent\UpdateLogs",

    [string] $MsiexecPath = "$env:WINDIR\System32\msiexec.exe",

    [string] $WebView2BootstrapperUri = "https://go.microsoft.com/fwlink/p/?LinkId=2124703",

    [string] $WebView2BootstrapperPath = "$env:ProgramData\AFK4\Agent\Prerequisites\MicrosoftEdgeWebView2Setup.exe",

    [string] $AgentServiceName = 'AFK4.Agent.Service',

    [switch] $SkipWebView2Prerequisite
)

$ErrorActionPreference = 'Stop'

function Test-WebView2RuntimeInstalled {
    $runtimeRegistryPaths = @(
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
        'HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
        'HKCU:\Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
    )

    foreach ($registryPath in $runtimeRegistryPaths) {
        $runtime = Get-ItemProperty -LiteralPath $registryPath -ErrorAction SilentlyContinue
        if ($null -ne $runtime -and
            -not [string]::IsNullOrWhiteSpace([string]$runtime.pv) -and
            [string]$runtime.pv -ne '0.0.0.0') {
            return $true
        }
    }

    return $false
}

function Install-WebView2Runtime {
    if (Test-WebView2RuntimeInstalled) {
        return
    }

    if ([string]::IsNullOrWhiteSpace($WebView2BootstrapperPath)) {
        throw 'WebView2BootstrapperPath is required when installing the Organization Admin component.'
    }

    if (-not (Test-Path -LiteralPath $WebView2BootstrapperPath)) {
        if ([string]::IsNullOrWhiteSpace($WebView2BootstrapperUri)) {
            throw 'WebView2 Runtime is missing and WebView2BootstrapperUri was not configured.'
        }

        $bootstrapperDirectory = Split-Path -Parent $WebView2BootstrapperPath
        New-Item -ItemType Directory -Force -Path $bootstrapperDirectory | Out-Null
        Invoke-WebRequest -Uri $WebView2BootstrapperUri -OutFile $WebView2BootstrapperPath -UseBasicParsing
    }

    $process = Start-Process `
        -FilePath $WebView2BootstrapperPath `
        -ArgumentList @('/silent', '/install') `
        -Wait `
        -PassThru `
        -WindowStyle Hidden

    if ($process.ExitCode -ne 0) {
        throw "WebView2 Runtime installer exited with code $($process.ExitCode)."
    }

    if (-not (Test-WebView2RuntimeInstalled)) {
        throw 'WebView2 Runtime installer completed, but the runtime registry marker was not found.'
    }
}

function Start-AgentServiceAfterSelfUpdate {
    param(
        [string] $Name
    )

    if ($Component -ne 'agent-service') {
        return
    }

    Start-Sleep -Seconds 5
    $service = Get-Service -Name $Name -ErrorAction Stop
    if ($service.Status -ne 'Running') {
        Start-Service -Name $Name -ErrorAction Stop
    }
}

if (-not (Test-Path -LiteralPath $PackagePath)) {
    throw "MSI package was not found at '$PackagePath'."
}

if ([System.IO.Path]::GetExtension($PackagePath) -ne '.msi') {
    throw "Update package must be an .msi file."
}

if (-not (Test-Path -LiteralPath $MsiexecPath)) {
    throw "msiexec was not found at '$MsiexecPath'."
}

if ($Component -eq 'organization-admin' -and -not $SkipWebView2Prerequisite) {
    Install-WebView2Runtime
}

New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
$safeComponent = $Component -replace '[^A-Za-z0-9_.-]', '_'
$safeVersion = $Version -replace '[^A-Za-z0-9_.-]', '_'
$logPath = Join-Path $LogDirectory "$safeComponent-$safeVersion-install.log"
$arguments = @('/i', $PackagePath, '/qn', '/norestart', '/l*v', $logPath)

$process = Start-Process -FilePath $MsiexecPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010) {
    Start-AgentServiceAfterSelfUpdate -Name $AgentServiceName
    exit 0
}

exit $process.ExitCode
