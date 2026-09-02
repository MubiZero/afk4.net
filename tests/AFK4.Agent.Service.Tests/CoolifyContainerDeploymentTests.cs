namespace AFK4.Agent.Service.Tests;

public sealed class CoolifyContainerDeploymentTests
{
    [Fact]
    public void PlatformApiDockerfile_UsesRepoRootContextAndRuntimePort()
    {
        var dockerfile = File.ReadAllText(RepositoryPath("src/AFK4.Platform.Api/Dockerfile"));

        Assert.Contains("mcr.microsoft.com/dotnet/sdk:10.0", dockerfile, StringComparison.Ordinal);
        Assert.Contains("mcr.microsoft.com/dotnet/aspnet:10.0", dockerfile, StringComparison.Ordinal);
        Assert.Contains("COPY [\"src/AFK4.Platform.Api/AFK4.Platform.Api.csproj\"", dockerfile, StringComparison.Ordinal);
        Assert.Contains("dotnet restore \"src/AFK4.Platform.Api/AFK4.Platform.Api.csproj\"", dockerfile, StringComparison.Ordinal);
        Assert.Contains("dotnet publish \"src/AFK4.Platform.Api/AFK4.Platform.Api.csproj\"", dockerfile, StringComparison.Ordinal);
        Assert.Contains("apt-get install -y --no-install-recommends curl wget", dockerfile, StringComparison.Ordinal);
        Assert.Contains("rm -rf /var/lib/apt/lists/*", dockerfile, StringComparison.Ordinal);
        Assert.Contains("ASPNETCORE_URLS=http://+:8080", dockerfile, StringComparison.Ordinal);
        Assert.Contains("EXPOSE 8080", dockerfile, StringComparison.Ordinal);
        Assert.Contains("USER app", dockerfile, StringComparison.Ordinal);
        Assert.Contains("ENTRYPOINT [\"dotnet\", \"AFK4.Platform.Api.dll\"]", dockerfile, StringComparison.Ordinal);
    }

    [Fact]
    public void Dockerignore_KeepsBuildContextSmallAndExcludesSecretShapedFiles()
    {
        var dockerignore = NormalizeLineEndings(File.ReadAllText(RepositoryPath(".dockerignore")));

        Assert.Contains(".git/", dockerignore, StringComparison.Ordinal);
        Assert.Contains("**/bin/", dockerignore, StringComparison.Ordinal);
        Assert.Contains("**/obj/", dockerignore, StringComparison.Ordinal);
        Assert.Contains("artifacts/", dockerignore, StringComparison.Ordinal);
        Assert.Contains("**/.env", dockerignore, StringComparison.Ordinal);
        Assert.Contains("**/.env.*", dockerignore, StringComparison.Ordinal);
        Assert.Contains("**/*.pfx", dockerignore, StringComparison.Ordinal);
        Assert.Contains("**/*.pem", dockerignore, StringComparison.Ordinal);
        Assert.Contains("**/*.key", dockerignore, StringComparison.Ordinal);
        Assert.Contains("**/secrets/", dockerignore, StringComparison.Ordinal);

        Assert.DoesNotContain("**/*password*", dockerignore, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("**/*secret*", dockerignore, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gitignore_ExcludesFilledEnvironmentFilesAndSecretMaterial()
    {
        var gitignore = NormalizeLineEndings(File.ReadAllText(RepositoryPath(".gitignore")));

        Assert.Contains(".env", gitignore, StringComparison.Ordinal);
        Assert.Contains(".env.*", gitignore, StringComparison.Ordinal);
        Assert.Contains("*.pfx", gitignore, StringComparison.Ordinal);
        Assert.Contains("*.p12", gitignore, StringComparison.Ordinal);
        Assert.Contains("*.pem", gitignore, StringComparison.Ordinal);
        Assert.Contains("*.key", gitignore, StringComparison.Ordinal);
        Assert.Contains("secrets/", gitignore, StringComparison.Ordinal);

        Assert.DoesNotContain("*password*", gitignore, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("*secret*", gitignore, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StagingEnvTemplate_UsesPlaceholdersForSecretsAndRequiredPlatformApiSettings()
    {
        var envTemplate = File.ReadAllText(RepositoryPath("deploy/coolify/staging.env.template"));

        Assert.Contains("ASPNETCORE_ENVIRONMENT=Staging", envTemplate, StringComparison.Ordinal);
        Assert.Contains("ASPNETCORE_URLS=http://+:8080", envTemplate, StringComparison.Ordinal);
        Assert.Contains("AllowedHosts=<coolify-staging-domain>;localhost;127.0.0.1", envTemplate, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__PlatformDatabase=", envTemplate, StringComparison.Ordinal);
        Assert.Contains("SSL Mode=Disable", envTemplate, StringComparison.Ordinal);
        Assert.Contains("GSS Encryption Mode=Disable", envTemplate, StringComparison.Ordinal);
        Assert.Contains("Sessions__SigningPrivateKeyPem=", envTemplate, StringComparison.Ordinal);
        Assert.Contains("Install__ApiBaseUrl=https://<coolify-staging-domain>", envTemplate, StringComparison.Ordinal);
        Assert.Contains("Install__UpdateChannel=internal", envTemplate, StringComparison.Ordinal);
        Assert.Contains("Install__UpdatePackageSigningPublicKeyPem=", envTemplate, StringComparison.Ordinal);
        Assert.Contains("runtime-only", envTemplate, StringComparison.Ordinal);
        Assert.Contains("<coolify", envTemplate, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("Host=localhost", envTemplate, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("afk4_dev", envTemplate, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Passw0rd", envTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("POSTGRES_HOST_AUTH_METHOD=trust", envTemplate, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StagingPostgresFallbackCompose_IsContainerizedStagingOnlyAndDoesNotExposePorts()
    {
        var compose = NormalizeLineEndings(File.ReadAllText(RepositoryPath("deploy/coolify/staging-postgres.fallback.compose.yaml")));

        Assert.Contains("postgres:17-alpine", compose, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_DB: ${AFK4_STAGING_POSTGRES_DB:-afk4_staging}", compose, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_USER: ${AFK4_STAGING_POSTGRES_USER:-afk4_app}", compose, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_PASSWORD: ${AFK4_STAGING_POSTGRES_PASSWORD:?", compose, StringComparison.Ordinal);
        Assert.Contains("pg_isready", compose, StringComparison.Ordinal);
        Assert.Contains("afk4-staging-postgres-data:", compose, StringComparison.Ordinal);

        Assert.DoesNotContain("POSTGRES_HOST_AUTH_METHOD", compose, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ports:", compose, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CoolifyStagingRunbook_CoversBuildDatabaseMigrationsHealthSmokeAndRollback()
    {
        var runbook = File.ReadAllText(RepositoryPath("docs/operations/coolify-staging-deploy.md"));

        Assert.Contains("Coolify-managed PostgreSQL", runbook, StringComparison.Ordinal);
        Assert.Contains("Build context", runbook, StringComparison.Ordinal);
        Assert.Contains("src/AFK4.Platform.Api/Dockerfile", runbook, StringComparison.Ordinal);
        Assert.Contains("deploy/coolify/staging.env.template", runbook, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__PlatformDatabase", runbook, StringComparison.Ordinal);
        Assert.Contains("Install__ApiBaseUrl=https://<coolify-staging-domain>", runbook, StringComparison.Ordinal);
        Assert.Contains("Install__UpdatePackageSigningPublicKeyPem", runbook, StringComparison.Ordinal);
        Assert.Contains("AllowedHosts=<coolify-staging-domain>;localhost;127.0.0.1", runbook, StringComparison.Ordinal);
        Assert.Contains("http://localhost:8080/api/health", runbook, StringComparison.Ordinal);
        Assert.Contains("runtime-only", runbook, StringComparison.Ordinal);
        Assert.Contains("curl", runbook, StringComparison.Ordinal);
        Assert.Contains("wget", runbook, StringComparison.Ordinal);
        Assert.Contains("SSL Mode=Disable", runbook, StringComparison.Ordinal);
        Assert.Contains("GSS Encryption Mode=Disable", runbook, StringComparison.Ordinal);
        Assert.Contains("dotnet ef database update", runbook, StringComparison.Ordinal);
        Assert.Contains("--idempotent", runbook, StringComparison.Ordinal);
        Assert.Contains("/api/health", runbook, StringComparison.Ordinal);
        Assert.Contains("Invoke-RestMethod", runbook, StringComparison.Ordinal);
        Assert.Contains("Rollback", runbook, StringComparison.Ordinal);
        Assert.Contains("Do not commit secrets", runbook, StringComparison.Ordinal);
    }

    /// <summary>
    /// Каждый явный источник COPY обязан существовать в репозитории.
    ///
    /// Один снесённый проект оставил в Dockerfile строку `COPY src/AFK4.BuildingBlocks/...`,
    /// и деплой staging упал уже после мержа: ни одна проверка PR в Dockerfile не заглядывает —
    /// `deploy/**` вообще не входит ни в один фильтр путей. Пусть заглядывает хотя бы этот тест.
    /// </summary>
    [Theory]
    [InlineData("src/AFK4.Platform.Api/Dockerfile")]
    [InlineData("deploy/coolify/platform-control.Dockerfile")]
    [InlineData("deploy/coolify/organization-admin.Dockerfile")]
    public void Dockerfile_CopiesOnlyPathsThatExist(string dockerfilePath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var lines = File.ReadAllLines(RepositoryPath(dockerfilePath));
        var missing = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("COPY ", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("--from=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var arguments = line["COPY ".Length..].Trim();
            var sources = arguments.StartsWith('[')
                ? arguments.Trim('[', ']').Split(',').Select(part => part.Trim().Trim('"')).ToArray()
                : arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Последний аргумент — путь внутри образа, а не в репозитории.
            foreach (var source in sources.Take(sources.Length - 1))
            {
                if (source is "." or ".." || source.Contains('*', StringComparison.Ordinal))
                {
                    continue;
                }

                var candidate = Path.Combine(repositoryRoot, source.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(candidate) && !Directory.Exists(candidate))
                {
                    missing.Add($"{dockerfilePath}: {source}");
                }
            }
        }

        Assert.Empty(missing);
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
