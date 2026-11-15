param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [Parameter(Mandatory = $true)]
    [string] $Component,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $LogDirectory = "$env:ProgramData\AFK4\Agent\UpdateLogs",

    [string] $MsiexecPath = "$env:WINDIR\System32\msiexec.exe",

    [string] $AgentServiceName = 'AFK4.Agent.Service'
)

$ErrorActionPreference = 'Stop'

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

New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
$safeComponent = $Component -replace '[^A-Za-z0-9_.-]', '_'
$safeVersion = $Version -replace '[^A-Za-z0-9_.-]', '_'
$logPath = Join-Path $LogDirectory "$safeComponent-$safeVersion-install.log"
$arguments = @('/i', $PackagePath, '/qn', '/norestart', '/l*v', $logPath)

# msiexec: установка легла, но занятые процессами файлы Windows заменит только при перезагрузке.
$rebootRequiredExitCode = 3010

$process = Start-Process -FilePath $MsiexecPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -eq 0 -or $process.ExitCode -eq $rebootRequiredExitCode) {
    Start-AgentServiceAfterSelfUpdate -Name $AgentServiceName
}

# Код возврата отдаётся агенту как есть. Раньше 3010 сводился к нулю, и платформа записывала
# «обновлено» по всему парку, который до перезагрузки работал на старой сборке.
exit $process.ExitCode
