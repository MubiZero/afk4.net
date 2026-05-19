param(
    [string] $SourceUrl,

    [string] $SourceUrlEnvVar = 'AFK4_BACKUP_SOURCE_URL',

    [string] $RestoreTargetUrl,

    [string] $RestoreTargetUrlEnvVar = 'AFK4_RESTORE_TARGET_URL',

    [string] $EfRestoreTargetConnectionString,

    [string] $EfRestoreTargetConnectionStringEnvVar,

    [string] $BackupRoot = 'D:\afk4-backups',

    [string] $BackupPath,

    [string] $MigrationScriptPath = 'artifacts\ef-migrations\afk4-platform-idempotent.sql',

    [string] $DotnetPath = 'C:\Program Files\dotnet\dotnet.exe',

    [string] $PgDumpPath = 'pg_dump',

    [string] $PgRestorePath = 'pg_restore',

    [string] $PsqlPath = 'psql',

    [ValidateSet('host', 'docker')]
    [string] $PostgresClientMode = 'host',

    [string] $PostgresDockerImage = 'postgres:17-alpine',

    [string] $DockerPath = 'docker',

    [switch] $DryRun,

    [switch] $SkipToolRestore,

    [switch] $SkipMigrationScript,

    [switch] $SkipDatabaseUpdate,

    [string[]] $SampleTables = @(
        'ledger_entries',
        'audit_records',
        'devices',
        'sessions',
        'update_packages')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepositoryRoot {
    $directory = [System.IO.DirectoryInfo]::new((Resolve-Path -LiteralPath $PSScriptRoot).Path)

    while ($null -ne $directory -and -not (Test-Path -LiteralPath (Join-Path $directory.FullName 'AFK4.sln') -PathType Leaf)) {
        $directory = $directory.Parent
    }

    if ($null -eq $directory) {
        throw 'Repository root was not found.'
    }

    return $directory.FullName
}

function Resolve-SecretValue {
    param(
        [string] $ExplicitValue,
        [string] $EnvironmentVariableName,
        [string] $Description
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitValue)) {
        return $ExplicitValue
    }

    if ([string]::IsNullOrWhiteSpace($EnvironmentVariableName)) {
        throw "$Description is required. Supply it directly or set an environment variable."
    }

    $environmentValue = [Environment]::GetEnvironmentVariable($EnvironmentVariableName)
    if ([string]::IsNullOrWhiteSpace($environmentValue)) {
        throw "$Description is required. Set environment variable '$EnvironmentVariableName' or pass the matching parameter."
    }

    return $environmentValue
}

function Redact-PostgresUrl {
    param(
        [string] $Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return '<empty>'
    }

    if ($Value.Contains(';')) {
        $segments = @()
        foreach ($segment in $Value.Split(';')) {
            if ($segment -match '^\s*(Password|Pwd)\s*=') {
                $segments += ($segment -replace '=(.*)$', '=<redacted>')
            }
            else {
                $segments += $segment
            }
        }

        return ($segments -join ';')
    }

    try {
        $uri = [uri] $Value
        if ($uri.IsAbsoluteUri -and -not [string]::IsNullOrWhiteSpace($uri.UserInfo)) {
            $builder = [System.UriBuilder]::new($uri)
            $builder.UserName = 'redacted'
            $builder.Password = 'redacted'
            return $builder.Uri.AbsoluteUri
        }
    }
    catch {
        return '<unparseable postgres URL redacted>'
    }

    return $Value
}

function Assert-PathOutsideRepository {
    param(
        [string] $RepositoryRoot,
        [string] $Path,
        [string] $Description
    )

    $repositoryFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $pathFullPath = [System.IO.Path]::GetFullPath($Path)
    $repositoryWithSeparator = $repositoryFullPath + [System.IO.Path]::DirectorySeparatorChar

    if ($pathFullPath.Equals($repositoryFullPath, [System.StringComparison]::OrdinalIgnoreCase) -or
        $pathFullPath.StartsWith($repositoryWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Do not commit database dumps. $Description must be outside repository root '$repositoryFullPath'."
    }

    return $pathFullPath
}

function Resolve-OutputFilePath {
    param(
        [string] $RepositoryRoot,
        [string] $Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
}

function Resolve-BackupFilePath {
    param(
        [string] $RepositoryRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($BackupPath)) {
        return Assert-PathOutsideRepository $RepositoryRoot $BackupPath 'BackupPath'
    }

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $candidate = Join-Path $BackupRoot "afk4-platform-$timestamp.dump"
    return Assert-PathOutsideRepository $RepositoryRoot $candidate 'BackupRoot'
}

function Resolve-ToolPath {
    param(
        [string] $PathOrName,
        [string] $Description
    )

    if ([string]::IsNullOrWhiteSpace($PathOrName)) {
        throw "$Description path is required."
    }

    if (Test-Path -LiteralPath $PathOrName -PathType Leaf) {
        return (Resolve-Path -LiteralPath $PathOrName).Path
    }

    $command = Get-Command $PathOrName -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "$Description '$PathOrName' was not found. Install PostgreSQL client tools or pass an explicit tool path."
    }

    return $command.Source
}

function Convert-PostgresUrlForDockerClient {
    param(
        [string] $Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Value
    }

    if ($Value.Contains(';')) {
        $segments = @()
        foreach ($segment in $Value.Split(';')) {
            if ($segment -match '^\s*(Host|Server)\s*=\s*(localhost|127\.0\.0\.1|\[::1\])\s*$') {
                $name = $Matches[1]
                $segments += "$name=host.docker.internal"
            }
            else {
                $segments += $segment
            }
        }

        return ($segments -join ';')
    }

    try {
        $uri = [uri] $Value
        if ($uri.IsAbsoluteUri -and
            ($uri.Host.Equals('localhost', [System.StringComparison]::OrdinalIgnoreCase) -or
                $uri.Host.Equals('127.0.0.1', [System.StringComparison]::OrdinalIgnoreCase) -or
                $uri.Host.Equals('[::1]', [System.StringComparison]::OrdinalIgnoreCase))) {
            $builder = [System.UriBuilder]::new($uri)
            $builder.Host = 'host.docker.internal'
            return $builder.Uri.AbsoluteUri
        }
    }
    catch {
        return $Value
    }

    return $Value
}

function Convert-BackupPathForDockerClient {
    param(
        [string] $Path
    )

    return '/backup/' + [System.IO.Path]::GetFileName($Path)
}

function Invoke-RehearsalCommand {
    param(
        [string] $Description,
        [string] $FilePath,
        [string[]] $Arguments,
        [switch] $SensitiveArguments
    )

    Write-Host $Description

    if ($DryRun) {
        if ($SensitiveArguments) {
            Write-Host "DRY RUN: would execute '$FilePath' with redacted arguments."
        }
        else {
            Write-Host "DRY RUN: $FilePath $($Arguments -join ' ')"
        }

        return
    }

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Invoke-PostgresClientCommand {
    param(
        [string] $Description,
        [string] $ClientTool,
        [string[]] $Arguments,
        [switch] $SensitiveArguments
    )

    if ($PostgresClientMode -eq 'host') {
        Invoke-RehearsalCommand `
            -Description $Description `
            -FilePath $ClientTool `
            -Arguments $Arguments `
            -SensitiveArguments:$SensitiveArguments
        return
    }

    Write-Host $Description

    $backupDirectory = [System.IO.Path]::GetDirectoryName($resolvedBackupPath)
    $dockerArguments = @(
        'run',
        '--rm',
        '--volume',
        "${backupDirectory}:/backup",
        $PostgresDockerImage,
        $ClientTool) + $Arguments

    if ($DryRun) {
        if ($SensitiveArguments) {
            Write-Host "DRY RUN: would execute '$DockerPath' with dockerized PostgreSQL client '$ClientTool' and redacted arguments."
        }
        else {
            Write-Host "DRY RUN: $DockerPath $($dockerArguments -join ' ')"
        }

        return
    }

    & $resolvedDockerPath @dockerArguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Assert-SafeTableName {
    param(
        [string] $TableName
    )

    if ([string]::IsNullOrWhiteSpace($TableName) -or $TableName -notmatch '^[a-z][a-z0-9_]*$') {
        throw "Sample table name '$TableName' is not safe."
    }
}

$repositoryRoot = Resolve-RepositoryRoot
$sourceDatabaseUrl = Resolve-SecretValue $SourceUrl $SourceUrlEnvVar 'Backup source PostgreSQL URL'
$restoreDatabaseUrl = Resolve-SecretValue $RestoreTargetUrl $RestoreTargetUrlEnvVar 'Restore target PostgreSQL URL'
if (-not [string]::IsNullOrWhiteSpace($EfRestoreTargetConnectionString) -or
    -not [string]::IsNullOrWhiteSpace($EfRestoreTargetConnectionStringEnvVar)) {
    $efRestoreTargetConnectionString = Resolve-SecretValue `
        $EfRestoreTargetConnectionString `
        $EfRestoreTargetConnectionStringEnvVar `
        'EF restore target PostgreSQL connection string'
}
else {
    $efRestoreTargetConnectionString = $restoreDatabaseUrl
}
$resolvedBackupPath = Resolve-BackupFilePath $repositoryRoot
$resolvedMigrationScriptPath = Resolve-OutputFilePath $repositoryRoot $MigrationScriptPath

Write-Host 'PostgreSQL restore rehearsal'
Write-Host "Source: $(Redact-PostgresUrl $sourceDatabaseUrl)"
Write-Host "Restore target: $(Redact-PostgresUrl $restoreDatabaseUrl)"
Write-Host "Backup path: $resolvedBackupPath"
Write-Host "Migration script path: $resolvedMigrationScriptPath"

if (-not $DryRun) {
    $backupDirectory = [System.IO.Path]::GetDirectoryName($resolvedBackupPath)
    $migrationScriptDirectory = [System.IO.Path]::GetDirectoryName($resolvedMigrationScriptPath)
    New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
    New-Item -ItemType Directory -Force -Path $migrationScriptDirectory | Out-Null
}

if ($DryRun) {
    $resolvedDotnetPath = $DotnetPath
    $resolvedPgDumpPath = $PgDumpPath
    $resolvedPgRestorePath = $PgRestorePath
    $resolvedPsqlPath = $PsqlPath
    $resolvedDockerPath = $DockerPath
}
else {
    $resolvedDotnetPath = Resolve-ToolPath $DotnetPath 'dotnet'
    if ($PostgresClientMode -eq 'docker') {
        $resolvedDockerPath = Resolve-ToolPath $DockerPath 'docker'
    }
    else {
        $resolvedPgDumpPath = Resolve-ToolPath $PgDumpPath 'pg_dump'
        $resolvedPgRestorePath = Resolve-ToolPath $PgRestorePath 'pg_restore'
        $resolvedPsqlPath = Resolve-ToolPath $PsqlPath 'psql'
    }
}

if ($PostgresClientMode -eq 'docker') {
    $postgresSourceDatabaseUrl = Convert-PostgresUrlForDockerClient $sourceDatabaseUrl
    $postgresRestoreDatabaseUrl = Convert-PostgresUrlForDockerClient $restoreDatabaseUrl
    $postgresBackupPath = Convert-BackupPathForDockerClient $resolvedBackupPath
    $postgresPgDumpPath = 'pg_dump'
    $postgresPgRestorePath = 'pg_restore'
    $postgresPsqlPath = 'psql'
}
else {
    $postgresSourceDatabaseUrl = $sourceDatabaseUrl
    $postgresRestoreDatabaseUrl = $restoreDatabaseUrl
    $postgresBackupPath = $resolvedBackupPath
    $postgresPgDumpPath = $resolvedPgDumpPath
    $postgresPgRestorePath = $resolvedPgRestorePath
    $postgresPsqlPath = $resolvedPsqlPath
}

if (-not $SkipToolRestore) {
    Invoke-RehearsalCommand `
        -Description 'Restore repository-local .NET tools.' `
        -FilePath $resolvedDotnetPath `
        -Arguments @('tool', 'restore')
}

Invoke-PostgresClientCommand `
    -Description 'Create custom-format PostgreSQL backup archive.' `
    -ClientTool $postgresPgDumpPath `
    -Arguments @(
        '--format=custom',
        '--no-owner',
        '--no-privileges',
        '--file',
        $postgresBackupPath,
        $postgresSourceDatabaseUrl) `
    -SensitiveArguments

Invoke-PostgresClientCommand `
    -Description 'Verify backup archive catalog is readable.' `
    -ClientTool $postgresPgRestorePath `
    -Arguments @('--list', $postgresBackupPath)

if (-not $SkipMigrationScript) {
    Invoke-RehearsalCommand `
        -Description 'Generate idempotent EF migration script for release review.' `
        -FilePath $resolvedDotnetPath `
        -Arguments @(
            'ef',
            'migrations',
            'script',
            '--project',
            'src/AFK4.Platform.Api/AFK4.Platform.Api.csproj',
            '--startup-project',
            'src/AFK4.Platform.Api/AFK4.Platform.Api.csproj',
            '--idempotent',
            '--output',
            $resolvedMigrationScriptPath)
}

Invoke-PostgresClientCommand `
    -Description 'Restore backup into rehearsal database.' `
    -ClientTool $postgresPgRestorePath `
    -Arguments @(
        '--clean',
        '--if-exists',
        '--no-owner',
        '--no-privileges',
        '--dbname',
        $postgresRestoreDatabaseUrl,
        $postgresBackupPath) `
    -SensitiveArguments

if (-not $SkipDatabaseUpdate) {
    Write-Host 'Apply EF migrations to restored rehearsal database.'

    if ($DryRun) {
        Write-Host 'DRY RUN: would set ConnectionStrings__PlatformDatabase to the redacted restore target and run dotnet ef database update.'
    }
    else {
        $previousConnectionString = [Environment]::GetEnvironmentVariable('ConnectionStrings__PlatformDatabase')
        try {
            [Environment]::SetEnvironmentVariable('ConnectionStrings__PlatformDatabase', $efRestoreTargetConnectionString, 'Process')
            Invoke-RehearsalCommand `
                -Description 'Run EF database update against restored rehearsal database.' `
                -FilePath $resolvedDotnetPath `
                -Arguments @(
                    'ef',
                    'database',
                    'update',
                    '--project',
                    'src/AFK4.Platform.Api/AFK4.Platform.Api.csproj',
                    '--startup-project',
                    'src/AFK4.Platform.Api/AFK4.Platform.Api.csproj')
        }
        finally {
            [Environment]::SetEnvironmentVariable('ConnectionStrings__PlatformDatabase', $previousConnectionString, 'Process')
        }
    }
}

foreach ($table in $SampleTables) {
    Assert-SafeTableName $table
    Invoke-PostgresClientCommand `
        -Description "Sample restored table count: $table" `
        -ClientTool $postgresPsqlPath `
        -Arguments @(
            $postgresRestoreDatabaseUrl,
            '-c',
            "select '$table' as table_name, count(*) as row_count from $table;") `
        -SensitiveArguments
}

Write-Host 'Restore rehearsal command sequence completed.'
