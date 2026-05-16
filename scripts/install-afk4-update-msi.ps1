param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [Parameter(Mandatory = $true)]
    [string] $Component,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $LogDirectory = "$env:ProgramData\AFK4\Agent\UpdateLogs",

    [string] $MsiexecPath = "$env:WINDIR\System32\msiexec.exe"
)

$ErrorActionPreference = 'Stop'

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

$process = Start-Process -FilePath $MsiexecPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010) {
    exit 0
}

exit $process.ExitCode
