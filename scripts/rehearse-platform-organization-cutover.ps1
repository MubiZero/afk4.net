[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ConnectionString
)

$ErrorActionPreference = 'Stop'

function Get-ConnectionValue([string] $Name) {
    $match = [regex]::Match($ConnectionString, "(?i)(?:^|;)\s*$Name\s*=\s*([^;]+)")
    if (-not $match.Success) { return $null }
    return $match.Groups[1].Value.Trim()
}

function Invoke-Checked([string] $Command, [string[]] $Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command failed with exit code $LASTEXITCODE."
    }
}

$database = Get-ConnectionValue 'Database'
if (-not $database) { $database = Get-ConnectionValue 'Initial Catalog' }
$hostName = Get-ConnectionValue 'Host'
if (-not $hostName) { $hostName = 'localhost' }
$port = Get-ConnectionValue 'Port'
if (-not $port) { $port = '5432' }
$userName = Get-ConnectionValue 'Username'
if (-not $userName) { $userName = Get-ConnectionValue 'User ID' }
$password = Get-ConnectionValue 'Password'

if (-not $database -or -not $userName) {
    throw 'ConnectionString must contain Database and Username (or User ID).'
}

foreach ($command in @('pg_dump', 'pg_restore', 'createdb', 'dropdb', 'psql', 'dotnet')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Required command '$command' is not available."
    }
}

$cloneDatabase = "afk4_cutover_rehearsal_$([Guid]::NewGuid().ToString('N'))"
$dumpFile = [System.IO.Path]::GetTempFileName()
$previousPgPassword = $env:PGPASSWORD
$env:PGPASSWORD = $password

$pgCommon = @('-h', $hostName, '-p', $port, '-U', $userName)
$cloneCreated = $false

try {
    Invoke-Checked 'pg_dump' ($pgCommon + @('-d', $database, '--format=custom', '--no-owner', '--file', $dumpFile))
    Invoke-Checked 'createdb' ($pgCommon + @($cloneDatabase))
    $cloneCreated = $true
    Invoke-Checked 'pg_restore' ($pgCommon + @('-d', $cloneDatabase, '--no-owner', $dumpFile))

    $deviceBefore = (& psql @pgCommon -d $cloneDatabase -t -A -c 'SELECT count(*) FROM device_credentials WHERE "RevokedAtUtc" IS NOT NULL;').Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Failed to read the pre-migration device credential count.' }

    $tempConnection = [regex]::Replace(
        $ConnectionString,
        '(?i)(Database|Initial Catalog)\s*=\s*[^;]+',
        "Database=$cloneDatabase",
        1)
    Invoke-Checked 'dotnet' @(
        'ef', 'database', 'update',
        '--project', 'src/AFK4.Platform.Api/AFK4.Platform.Api.csproj',
        '--startup-project', 'src/AFK4.Platform.Api/AFK4.Platform.Api.csproj',
        '--connection', $tempConnection)

    $checks = [ordered]@{
        unknownOrganizationRoles = @'
SELECT count(*) FROM (
  SELECT "RoleName" AS role_name FROM staff_role_assignments
  UNION ALL SELECT "RoleName" FROM staff_money_caps
  UNION ALL SELECT btrim(role_name) FROM staff_invites, LATERAL regexp_split_to_table("RoleNamesCsv", ',') role_name
) roles WHERE role_name NOT IN ('organization_owner','branch_manager','shift_supervisor','operator','technician','accountant');
'@
        unknownPlatformRoles = @'
SELECT count(*) FROM platform_admin_users, LATERAL jsonb_array_elements_text("RolesJson") role_name
WHERE role_name NOT IN ('platform_admin','platform_support');
'@
        activeInteractiveTokens = @'
SELECT
  (SELECT count(*) FROM staff_access_tokens WHERE "RevokedAtUtc" IS NULL) +
  (SELECT count(*) FROM staff_refresh_tokens WHERE "RevokedAtUtc" IS NULL) +
  (SELECT count(*) FROM platform_admin_access_tokens WHERE "RevokedAtUtc" IS NULL) +
  (SELECT count(*) FROM platform_admin_refresh_tokens WHERE "RevokedAtUtc" IS NULL) +
  (SELECT count(*) FROM player_access_tokens WHERE "RevokedAtUtc" IS NULL) +
  (SELECT count(*) FROM player_refresh_tokens WHERE "RevokedAtUtc" IS NULL);
'@
        revokedDeviceCredentials = 'SELECT count(*) FROM device_credentials WHERE "RevokedAtUtc" IS NOT NULL;'
    }

    foreach ($check in $checks.GetEnumerator()) {
        $value = (& psql @pgCommon -d $cloneDatabase -t -A -c $check.Value).Trim()
        if ($LASTEXITCODE -ne 0) { throw "Verification query '$($check.Key)' failed." }
        Write-Output "$($check.Key)=$value"
        if ($check.Key -ne 'revokedDeviceCredentials' -and $value -ne '0') {
            throw "Verification '$($check.Key)' returned $value instead of 0."
        }
        if ($check.Key -eq 'revokedDeviceCredentials' -and $value -ne $deviceBefore) {
            throw 'Device credential revocation state changed during the rehearsal.'
        }
    }
}
finally {
    if ($cloneCreated) {
        & dropdb @pgCommon --if-exists $cloneDatabase | Out-Null
    }
    Remove-Item -LiteralPath $dumpFile -Force -ErrorAction SilentlyContinue
    $env:PGPASSWORD = $previousPgPassword
}
