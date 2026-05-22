param(
    [string] $ConnectionString = $env:ConnectionStrings__PlatformDatabase,
    [string] $OperatorPassword = $(if ($env:AFK4_DEV_SEED_OPERATOR_PASSWORD) { $env:AFK4_DEV_SEED_OPERATOR_PASSWORD } else { 'Passw0rd!' }),
    [switch] $NoReset
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\AFK4.Platform.DevSeed\AFK4.Platform.DevSeed.csproj'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'

$arguments = @('run', '--project', $projectPath, '--')

if ($ConnectionString) {
    $arguments += @('--connection-string', $ConnectionString)
}

if ($OperatorPassword) {
    $arguments += @('--password', $OperatorPassword)
}

if ($NoReset) {
    $arguments += '--no-reset'
}

& $dotnet @arguments

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
