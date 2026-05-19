using System.Management.Automation.Language;

namespace AFK4.Agent.Service.Tests;

public sealed class PostgresRestoreRehearsalScriptTests
{
    [Fact]
    public void PostgresRestoreRehearsalScript_ParsesRequiredParameters()
    {
        var ast = Parser.ParseFile(RepositoryPath("scripts/rehearse-postgres-restore.ps1"), out _, out var errors);

        Assert.Empty(errors);
        AssertParameter(ast, "SourceUrl");
        AssertParameter(ast, "SourceUrlEnvVar");
        AssertParameter(ast, "RestoreTargetUrl");
        AssertParameter(ast, "RestoreTargetUrlEnvVar");
        AssertParameter(ast, "EfRestoreTargetConnectionString");
        AssertParameter(ast, "EfRestoreTargetConnectionStringEnvVar");
        AssertParameter(ast, "BackupRoot");
        AssertParameter(ast, "BackupPath");
        AssertParameter(ast, "MigrationScriptPath");
        AssertParameter(ast, "DotnetPath");
        AssertParameter(ast, "PgDumpPath");
        AssertParameter(ast, "PgRestorePath");
        AssertParameter(ast, "PsqlPath");
        AssertParameter(ast, "PostgresClientMode");
        AssertParameter(ast, "PostgresDockerImage");
        AssertParameter(ast, "DockerPath");
        AssertParameter(ast, "DryRun");
        AssertParameter(ast, "SkipToolRestore");
        AssertParameter(ast, "SkipMigrationScript");
        AssertParameter(ast, "SkipDatabaseUpdate");
        AssertParameter(ast, "SampleTables");
    }

    [Fact]
    public void PostgresRestoreRehearsalScript_RedactsSecretsAndProtectsRepository()
    {
        var script = NormalizeLineEndings(File.ReadAllText(RepositoryPath("scripts/rehearse-postgres-restore.ps1")));

        Assert.Contains("AFK4_BACKUP_SOURCE_URL", script, StringComparison.Ordinal);
        Assert.Contains("AFK4_RESTORE_TARGET_URL", script, StringComparison.Ordinal);
        Assert.Contains("function Redact-PostgresUrl", script, StringComparison.Ordinal);
        Assert.Contains("function Assert-PathOutsideRepository", script, StringComparison.Ordinal);
        Assert.Contains("Do not commit database dumps", script, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__PlatformDatabase", script, StringComparison.Ordinal);
        Assert.Contains("$efRestoreTargetConnectionString", script, StringComparison.Ordinal);
        Assert.Contains("ledger_entries", script, StringComparison.Ordinal);
        Assert.Contains("audit_records", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Write-Host $SourceUrl", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Write-Host $RestoreTargetUrl", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PostgresRestoreRehearsalScript_CanUseDockerizedPostgresClientTools()
    {
        var script = NormalizeLineEndings(File.ReadAllText(RepositoryPath("scripts/rehearse-postgres-restore.ps1")));

        Assert.Contains("[ValidateSet('host', 'docker')]", script, StringComparison.Ordinal);
        Assert.Contains("postgres:17-alpine", script, StringComparison.Ordinal);
        Assert.Contains("function Convert-PostgresUrlForDockerClient", script, StringComparison.Ordinal);
        Assert.Contains("host.docker.internal", script, StringComparison.Ordinal);
        Assert.Contains("function Convert-BackupPathForDockerClient", script, StringComparison.Ordinal);
        Assert.Contains("--volume", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-PostgresClientCommand", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PostgresBackupRestoreRunbook_PointsToRehearsalScript()
    {
        var runbook = NormalizeLineEndings(File.ReadAllText(RepositoryPath("docs/operations/postgres-backup-restore.md")));

        Assert.Contains("scripts/rehearse-postgres-restore.ps1", runbook, StringComparison.Ordinal);
        Assert.Contains("-DryRun", runbook, StringComparison.Ordinal);
        Assert.Contains("-PostgresClientMode docker", runbook, StringComparison.Ordinal);
        Assert.Contains("-EfRestoreTargetConnectionString", runbook, StringComparison.Ordinal);
        Assert.Contains("AFK4_BACKUP_SOURCE_URL", runbook, StringComparison.Ordinal);
        Assert.Contains("AFK4_RESTORE_TARGET_URL", runbook, StringComparison.Ordinal);
    }

    private static void AssertParameter(ScriptBlockAst ast, string name)
    {
        Assert.NotNull(ast.ParamBlock);
        Assert.Contains(
            ast.ParamBlock!.Parameters,
            parameter => string.Equals(
                parameter.Name.VariablePath.UserPath,
                name,
                StringComparison.Ordinal));
    }

    private static string RepositoryPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), relativePath));
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AFK4.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Repository root was not found.");
        }

        return directory.FullName;
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
    }
}
